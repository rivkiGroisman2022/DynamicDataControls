using DataControls.Core.Definitions;
using DataControls.Core.Validation;

namespace DataControls.Core.Controls;

// The objects that cross the API boundary (Data Transfer Objects).

public sealed record ControlUpsertRequest(
    string Name,
    string? Description,
    string? Severity,
    bool IsActive,
    string? Source,
    ControlDefinition Definition);

public sealed record ControlListItemDto(
    int Id,
    string Name,
    string? Description,
    string EntityKey,
    string EntityDisplayName,
    string DefinitionText,
    string Severity,
    string Source,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    int? LastViolationCount);

public sealed record ControlDetailsDto(
    int Id,
    string Name,
    string? Description,
    string EntityKey,
    string EntityDisplayName,
    string DefinitionText,
    string Severity,
    string Source,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    ControlDefinition Definition);

/// <summary>Result of checking a definition without saving it: is it valid, what it means in words, and the SQL (for transparency).</summary>
public sealed record DefinitionPreviewDto(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    string? DefinitionText,
    string? Sql);

public sealed record RunColumnDto(
    string Key,
    string DisplayName,
    string GroupLabel,
    string DataType,
    string? Unit,
    bool IsRuleField,
    IReadOnlyList<Metadata.FieldOption> Options);

public sealed record ViolationRowDto(
    string RecordId,
    string Title,
    IReadOnlyDictionary<string, object?> Values,
    IReadOnlyList<string> Reasons,
    IReadOnlyList<int> MatchedRules);

/// <summary>A single condition in the control, for filtering results by reason ("only the records that failed on salary").</summary>
public sealed record RunRuleDto(int Index, string Text);

public sealed record ControlRunResultDto(
    int? ControlId,
    string ControlName,
    string EntityKey,
    string EntityDisplayName,
    string DefinitionText,
    int TotalViolations,
    int ReturnedRows,
    bool IsTruncated,
    long DurationMs,
    DateTime RunAt,
    IReadOnlyList<RunColumnDto> Columns,
    IReadOnlyList<RunRuleDto> Rules,
    IReadOnlyList<ViolationRowDto> Rows);
