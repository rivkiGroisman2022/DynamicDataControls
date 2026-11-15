using DataControls.Core.Definitions;

namespace DataControls.Core.Controls;

public static class ControlSeverity
{
    public static readonly string[] All = ["low", "medium", "high"];
    public static bool IsValid(string? value) => value is not null && All.Contains(value);
}

public static class ControlSource
{
    public const string Manual = "manual";
    public const string Ai = "ai";
    public static bool IsValid(string? value) => value is Manual or Ai;
}

/// <summary>A saved control.</summary>
public sealed record Control(
    int Id,
    string Name,
    string? Description,
    string EntityKey,
    ControlDefinition Definition,
    string Severity,
    string Source,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>A row in the control list, with the result of the last run.</summary>
public sealed record ControlSummary(
    int Id,
    string Name,
    string? Description,
    string EntityKey,
    string DefinitionJson,
    string Severity,
    string Source,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    int? LastViolationCount);

/// <summary>What is written to the database when creating or updating.</summary>
public sealed record ControlWriteModel(
    string Name,
    string? Description,
    string EntityKey,
    string DefinitionJson,
    string Severity,
    string Source,
    bool IsActive);

public sealed record ControlRunRecord(
    int ControlId,
    DateTime StartedAt,
    int DurationMs,
    string Status,
    int? ViolationCount,
    string? ErrorMessage);

public sealed record ControlRunHistoryItem(
    long RunId,
    DateTime StartedAt,
    int? DurationMs,
    string Status,
    int? ViolationCount,
    string? ErrorMessage);
