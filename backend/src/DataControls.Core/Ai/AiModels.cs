using DataControls.Core.Definitions;
using DataControls.Core.Metadata;
using DataControls.Core.Validation;

namespace DataControls.Core.Ai;

// =====================================================================
// The contract with the AI service (in both directions).
// The AI never receives table names or SQL - only the semantic layer, in business terms.
// The AI never returns SQL - only a control definition in the same JSON format the UI produces.
// =====================================================================

/// <summary>What the AI model is told about the data: entities, fields (including through relations), and operators.</summary>
public sealed record AiSchemaContext(
    string Language,
    string Today,
    IReadOnlyList<AiEntityContext> Entities,
    IReadOnlyList<OperatorDto> Operators);

public sealed record AiEntityContext(
    string Key,
    string DisplayName,
    string DisplayNamePlural,
    string? Description,
    IReadOnlyList<string> Synonyms,
    IReadOnlyList<AiFieldContext> Fields);

/// <param name="Path">The path the model must return in the definition ("department.isActive").</param>
/// <param name="EntitySynonyms">Synonyms of the entity that owns the field - helps tell "פעיל" of an employee from "פעילה" of a department.</param>
public sealed record AiFieldContext(
    string Path,
    string DisplayName,
    string EntityKey,
    string EntityDisplayName,
    string GroupLabel,
    string DataType,
    string? Unit,
    string? Description,
    IReadOnlyList<string> Synonyms,
    IReadOnlyList<string> EntitySynonyms,
    IReadOnlyList<FieldOption> Options,
    IReadOnlyList<string> Operators);

public sealed record AiSuggestControlRequest(string Text, AiSchemaContext Schema);

public sealed record AiQualityRequest(AiSchemaContext Schema, string? Entity);

/// <summary>The structure of the answer expected from the AI (the response contract).</summary>
/// <param name="Explanation">How the model understood the request, step by step - shown to the user for transparency.</param>
/// <param name="Confidence">0..1 - how confident the model is in the interpretation.</param>
/// <param name="Warnings">Parts of the request that were not understood or were interpreted under an assumption.</param>
public sealed record AiControlSuggestion(
    string Name,
    string? Description,
    string? Severity,
    ControlDefinition Definition,
    IReadOnlyList<string>? Explanation,
    double Confidence,
    IReadOnlyList<string>? Warnings,
    string Provider,
    string? Model);

public sealed record AiQualityResponse(IReadOnlyList<AiControlSuggestion> Suggestions);

// ---------- What is returned to the UI after server-side validation ----------

public sealed record SuggestControlRequestDto(string Text);

public sealed record QualitySuggestionsRequestDto(string? Entity);

/// <summary>An AI suggestion after it passed the same validator as any manual control, with a description in words for user approval.</summary>
public sealed record AiSuggestionDto(
    string Name,
    string? Description,
    string Severity,
    ControlDefinition Definition,
    string? DefinitionText,
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<string> Explanation,
    double Confidence,
    IReadOnlyList<string> Warnings,
    string Provider,
    string? Model);
