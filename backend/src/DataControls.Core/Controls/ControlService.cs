using DataControls.Core.Abstractions;
using DataControls.Core.Definitions;
using DataControls.Core.Describing;
using DataControls.Core.Exceptions;
using DataControls.Core.Metadata;
using DataControls.Core.Query;
using DataControls.Core.Validation;

namespace DataControls.Core.Controls;

/// <summary>Managing control definitions: list, create, update, delete, preview.</summary>
public sealed class ControlService(IControlRepository repository, IMetadataProvider metadata)
{
    private readonly ControlDefinitionValidator _validator = new();
    private readonly SqlQueryBuilder _builder = new();

    public async Task<IReadOnlyList<ControlListItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        var catalog = await metadata.GetCatalogAsync(ct);
        var controls = await repository.GetAllAsync(ct);
        return controls.Select(c => ToListItem(c, catalog)).ToList();
    }

    public async Task<ControlDetailsDto> GetAsync(int id, CancellationToken ct = default)
    {
        var control = await repository.GetByIdAsync(id, ct) ?? throw NotFound(id);
        var catalog = await metadata.GetCatalogAsync(ct);
        return ToDetails(control, catalog);
    }

    public async Task<ControlDetailsDto> CreateAsync(ControlUpsertRequest request, CancellationToken ct = default)
    {
        var write = await PrepareWriteAsync(request, existingId: null, ct);
        var id = await repository.CreateAsync(write, ct);
        return await GetAsync(id, ct);
    }

    public async Task<ControlDetailsDto> UpdateAsync(int id, ControlUpsertRequest request, CancellationToken ct = default)
    {
        var write = await PrepareWriteAsync(request, existingId: id, ct);
        if (!await repository.UpdateAsync(id, write, ct)) throw NotFound(id);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        if (!await repository.DeleteAsync(id, ct)) throw NotFound(id);
    }

    /// <summary>Checks a definition without saving: errors, a description in words and the SQL that would be produced.</summary>
    public async Task<DefinitionPreviewDto> PreviewAsync(ControlDefinition definition, bool includeSql, CancellationToken ct = default)
    {
        var catalog = await metadata.GetCatalogAsync(ct);
        var validation = _validator.Validate(definition, catalog);
        if (!validation.IsValid)
            return new DefinitionPreviewDto(false, validation.Errors, null, null);

        var sql = includeSql ? _builder.Build(definition, catalog).Sql : null;
        return new DefinitionPreviewDto(true, [], DefinitionDescriber.Describe(definition, catalog), sql);
    }

    private async Task<ControlWriteModel> PrepareWriteAsync(ControlUpsertRequest request, int? existingId, CancellationToken ct)
    {
        var errors = new List<ValidationError>();
        var name = request.Name?.Trim() ?? string.Empty;

        if (name.Length == 0) errors.Add(new("name", "יש להזין שם לבקרה."));
        else if (name.Length > 200) errors.Add(new("name", "שם הבקרה ארוך מדי (עד 200 תווים)."));

        if (request.Description is { Length: > 1000 })
            errors.Add(new("description", "התיאור ארוך מדי (עד 1000 תווים)."));

        var severity = request.Severity ?? "medium";
        if (!ControlSeverity.IsValid(severity)) errors.Add(new("severity", "רמת חומרה לא חוקית."));

        var source = request.Source ?? ControlSource.Manual;
        if (!ControlSource.IsValid(source)) errors.Add(new("source", "מקור לא חוקי."));

        var catalog = await metadata.GetCatalogAsync(ct);
        errors.AddRange(_validator.Validate(request.Definition, catalog).Errors
            .Select(e => e with { Path = string.IsNullOrEmpty(e.Path) ? "definition" : $"definition.{e.Path}" }));

        if (errors.Count > 0) throw new DefinitionValidationException(errors);

        if (await repository.NameExistsAsync(name, existingId, ct))
            throw new ConflictException($"כבר קיימת בקרה בשם '{name}'.");

        return new ControlWriteModel(
            name,
            string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            catalog.GetEntity(request.Definition.Entity).Key,
            DefinitionJson.Serialize(request.Definition),
            severity,
            source,
            request.IsActive);
    }

    private static ControlListItemDto ToListItem(ControlSummary c, MetadataCatalog catalog)
    {
        string text;
        try { text = DefinitionDescriber.Describe(DefinitionJson.Deserialize(c.DefinitionJson), catalog); }
        catch (InvalidDefinitionFormatException) { text = "הגדרה פגומה"; }

        return new ControlListItemDto(c.Id, c.Name, c.Description, c.EntityKey,
            catalog.FindEntity(c.EntityKey)?.DisplayNamePlural ?? c.EntityKey,
            text, c.Severity, c.Source, c.IsActive, c.CreatedAt, c.UpdatedAt,
            c.LastRunAt, c.LastRunStatus, c.LastViolationCount);
    }

    private static ControlDetailsDto ToDetails(Control c, MetadataCatalog catalog) =>
        new(c.Id, c.Name, c.Description, c.EntityKey,
            catalog.FindEntity(c.EntityKey)?.DisplayNamePlural ?? c.EntityKey,
            DefinitionDescriber.Describe(c.Definition, catalog),
            c.Severity, c.Source, c.IsActive, c.CreatedAt, c.UpdatedAt, c.Definition);

    private static NotFoundException NotFound(int id) => new($"בקרה מספר {id} לא נמצאה.");
}
