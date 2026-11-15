using DataControls.Core.Definitions;
using DataControls.Core.Metadata;

namespace DataControls.Core.Query;

/// <summary>A query parameter. Core does not depend on SqlClient, so the type is described with FieldDataType.</summary>
public sealed record QueryParameter(string Name, object? Value, FieldDataType Type);

/// <summary>A column returned by the query, with its business meaning.</summary>
/// <param name="Alias">The technical alias in the query (c0, c1...).</param>
/// <param name="Path">The business path ("department.isActive"), used as the key in the API.</param>
public sealed record ResultColumn(
    string Alias,
    string Path,
    ResolvedField Field,
    string GroupLabel,
    bool IsRuleField,
    bool IsTitle);

/// <summary>
/// "Probe" for one rule: a 0/1 column telling, per row, whether this specific rule matched.
/// This is how the reason for a violation is produced without running a separate query per rule.
/// </summary>
public sealed record RuleProbe(
    int Index,
    string FlagAlias,
    ConditionRule Rule,
    ResolvedField Field,
    ResolvedField? CompareField,
    bool IsUnderNegation);

/// <summary>
/// Output of the query builder: SQL text with no user values at all, plus the parameter list.
/// Separating definition → query → execution: whoever builds does not run, and whoever runs does not know the definition.
/// </summary>
public sealed record BuiltQuery(
    string Sql,
    IReadOnlyList<QueryParameter> Parameters,
    EntityMeta Entity,
    IReadOnlyList<ResultColumn> Columns,
    IReadOnlyList<RuleProbe> Rules,
    int MaxRows)
{
    public const string IdAlias = "__id";
    public const string TotalAlias = "__total";
}
