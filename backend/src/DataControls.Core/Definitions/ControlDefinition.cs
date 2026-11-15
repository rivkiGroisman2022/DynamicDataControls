using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataControls.Core.Definitions;

/// <summary>
/// The control definition: a data-only description of "which records count as exceptions".
/// It holds no SQL and no table names, only business keys taken from the metadata layer.
/// The same contract is shared by the UI, the AI service and the query engine.
/// </summary>
public sealed record ControlDefinition
{
    public int Version { get; init; } = 1;

    /// <summary>EntityKey of the business entity the control runs on (e.g. "Employee").</summary>
    public string Entity { get; init; } = string.Empty;

    /// <summary>Root of the condition tree. A record matching it is a violation.</summary>
    public ConditionNode Condition { get; init; } = new ConditionGroup();
}

/// <summary>A node in the condition tree: a group (AND/OR) or a single rule.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ConditionGroup), "group")]
[JsonDerivedType(typeof(ConditionRule), "rule")]
public abstract record ConditionNode;

public sealed record ConditionGroup : ConditionNode
{
    /// <summary>"and" or "or".</summary>
    public string Logic { get; init; } = LogicalOperators.And;

    /// <summary>When true the whole group is negated (NOT).</summary>
    public bool Negate { get; init; }

    public List<ConditionNode> Conditions { get; init; } = [];
}

public sealed record ConditionRule : ConditionNode
{
    /// <summary>Field path: "salary" on the entity itself, or "department.branch.isActive" through relations.</summary>
    public string Field { get; init; } = string.Empty;

    /// <summary>Operator key from <see cref="Operators.OperatorCatalog"/>.</summary>
    public string Operator { get; init; } = string.Empty;

    /// <summary>Constant value (number / string / date / bool). JsonElement because the type depends on the field.</summary>
    public JsonElement? Value { get; init; }

    /// <summary>Upper bound for "between".</summary>
    public JsonElement? ValueTo { get; init; }

    /// <summary>Value list for "in" / "notIn".</summary>
    public List<JsonElement>? Values { get; init; }

    /// <summary>Compare against another field instead of a constant (e.g. endDate &lt; startDate).</summary>
    public string? CompareField { get; init; }
}

public static class LogicalOperators
{
    public const string And = "and";
    public const string Or = "or";

    public static bool IsValid(string? logic) =>
        string.Equals(logic, And, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(logic, Or, StringComparison.OrdinalIgnoreCase);

    public static bool IsOr(string? logic) => string.Equals(logic, Or, StringComparison.OrdinalIgnoreCase);
}
