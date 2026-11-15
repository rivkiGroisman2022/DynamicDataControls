using DataControls.Core.Abstractions;
using DataControls.Core.Controls;
using DataControls.Core.Describing;
using DataControls.Core.Exceptions;
using DataControls.Core.Metadata;
using DataControls.Core.Validation;

namespace DataControls.Core.Ai;

/// <summary>
/// The access point to the AI service. Core does not know which model or vendor is behind it -
/// replacing the model (Mock → Claude → another model) = a different implementation of this interface or a setting in the AI service.
/// </summary>
public interface IAiServiceClient
{
    Task<AiControlSuggestion> SuggestControlAsync(AiSuggestControlRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<AiControlSuggestion>> SuggestQualityControlsAsync(AiQualityRequest request, CancellationToken ct = default);
}

/// <summary>
/// Stage B flow: free text → context (metadata) → AI → suggestion → validation → description in words → user approval.
/// Key principle: the AI only "suggests". Its output passes through exactly the same validator as a manual control,
/// nothing runs before the user approves, and running goes through the same engine (parameterized SQL, read-only user).
/// So even a "wrong" or "malicious" AI cannot cause harm - at most it suggests a control that the validator rejects.
/// </summary>
public sealed class AiAssistantService(IMetadataProvider metadata, IAiServiceClient ai, TimeProvider clock)
{
    public const int MaxTextLength = 1000;
    private readonly ControlDefinitionValidator _validator = new();

    public async Task<AiSuggestionDto> SuggestControlAsync(string? text, CancellationToken ct = default)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new DefinitionValidationException([new("text", "יש לתאר את הבקרה במילים.")]);
        if (trimmed.Length > MaxTextLength)
            throw new DefinitionValidationException([new("text", $"התיאור ארוך מדי (עד {MaxTextLength} תווים).")]);

        var catalog = await metadata.GetCatalogAsync(ct);
        var suggestion = await ai.SuggestControlAsync(new AiSuggestControlRequest(trimmed, BuildContext(catalog)), ct);
        return ToDto(suggestion, catalog);
    }

    /// <summary>Advanced (optional) task: the AI analyzes the data structure and proactively suggests data-quality controls.</summary>
    public async Task<IReadOnlyList<AiSuggestionDto>> SuggestQualityControlsAsync(string? entity, CancellationToken ct = default)
    {
        var catalog = await metadata.GetCatalogAsync(ct);
        if (entity is not null && catalog.FindEntity(entity) is null)
            throw new NotFoundException($"הישות '{entity}' אינה מוגדרת.");

        var suggestions = await ai.SuggestQualityControlsAsync(new AiQualityRequest(BuildContext(catalog), entity), ct);
        // Only valid suggestions are shown - a proactive suggestion that does not fit the structure has no value to the user.
        return suggestions.Select(s => ToDto(s, catalog)).Where(s => s.IsValid).ToList();
    }

    /// <summary>The context sent to the AI - exposed in the API too, for transparency ("what exactly does the model see").</summary>
    public async Task<AiSchemaContext> GetContextAsync(CancellationToken ct = default) =>
        BuildContext(await metadata.GetCatalogAsync(ct));

    private AiSchemaContext BuildContext(MetadataCatalog catalog) =>
        AiSchemaContextBuilder.Build(catalog, DateOnly.FromDateTime(clock.GetLocalNow().DateTime));

    private AiSuggestionDto ToDto(AiControlSuggestion s, MetadataCatalog catalog)
    {
        var validation = _validator.Validate(s.Definition, catalog);
        var severity = ControlSeverity.IsValid(s.Severity) ? s.Severity! : "medium";
        var name = string.IsNullOrWhiteSpace(s.Name) ? "בקרה שנוצרה ב-AI" : s.Name.Trim();
        if (name.Length > 200) name = name[..200];

        return new AiSuggestionDto(
            name,
            s.Description,
            severity,
            s.Definition,
            validation.IsValid ? DefinitionDescriber.Describe(s.Definition, catalog) : null,
            validation.IsValid,
            validation.Errors,
            s.Explanation ?? [],
            Math.Clamp(s.Confidence, 0, 1),
            s.Warnings ?? [],
            s.Provider,
            s.Model);
    }
}

/// <summary>Turns the semantic layer into context for the AI. Includes only business terms - no table or column names.</summary>
public static class AiSchemaContextBuilder
{
    public static AiSchemaContext Build(MetadataCatalog catalog, DateOnly today) => new(
        "he",
        today.ToString("yyyy-MM-dd"),
        catalog.Entities
            .Where(e => e.IsControlTarget)
            .OrderBy(e => e.Key)
            .Select(e => new AiEntityContext(
                e.Key, e.DisplayName, e.DisplayNamePlural, e.Description, e.Synonyms,
                catalog.GetReachableFields(e.Key).Select(f => new AiFieldContext(
                    f.Path,
                    f.Field.DisplayName,
                    f.Entity.Key,
                    f.Entity.DisplayName,
                    f.GroupLabel(e),
                    f.Field.DataType.ToString().ToLowerInvariant(),
                    f.Field.Unit,
                    f.Field.Description,
                    f.Field.Synonyms,
                    [f.Entity.DisplayName, f.Entity.DisplayNamePlural, .. f.Entity.Synonyms],
                    f.Field.Options,
                    Operators.OperatorCatalog.ForType(f.Field.DataType).Select(o => o.Key).ToList())).ToList()))
            .ToList(),
        MetadataService.GetOperators());
}
