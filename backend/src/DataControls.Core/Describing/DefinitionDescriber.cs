using System.Text.Json;
using DataControls.Core.Definitions;
using DataControls.Core.Metadata;
using DataControls.Core.Operators;
using DataControls.Core.Validation;

namespace DataControls.Core.Describing;

/// <summary>
/// Turns a control definition into a readable Hebrew sentence, for example:
/// "עובדים שבהם: גיל קטן מ־18 וגם שכר חודשי גדול מ־15,000 ש"ח".
/// Used in the control list, in previews, and mainly in stage B: before running an AI-generated control,
/// the user sees in plain words what the AI understood, and approves it.
/// </summary>
public static class DefinitionDescriber
{
    public static string Describe(ControlDefinition definition, MetadataCatalog catalog)
    {
        var entity = catalog.FindEntity(definition.Entity);
        if (entity is null) return "הגדרה לא תקינה";

        return $"{entity.DisplayNamePlural} – {DescribeNode(definition.Condition, catalog, entity, isRoot: true)}";
    }

    private static string DescribeNode(ConditionNode node, MetadataCatalog catalog, EntityMeta entity, bool isRoot) =>
        node switch
        {
            ConditionGroup g => DescribeGroup(g, catalog, entity, isRoot),
            ConditionRule r => DescribeRule(r, catalog, entity),
            _ => "?"
        };

    private static string DescribeGroup(ConditionGroup group, MetadataCatalog catalog, EntityMeta entity, bool isRoot)
    {
        var separator = LogicalOperators.IsOr(group.Logic) ? " או " : " וגם ";
        var parts = group.Conditions.Select(c => DescribeNode(c, catalog, entity, isRoot: false)).ToList();
        var text = string.Join(separator, parts);

        if (group.Negate) return $"לא מתקיים ש({text})";
        return isRoot || parts.Count == 1 ? text : $"({text})";
    }

    public static string DescribeRule(ConditionRule rule, MetadataCatalog catalog, EntityMeta entity)
    {
        if (!catalog.TryResolvePath(entity.Key, rule.Field, out var field, out _))
            return $"[שדה לא מוכר: {rule.Field}]";

        if (OperatorCatalog.Find(rule.Operator) is null)
            return $"{field!.Field.DisplayName} [פעולה לא מוכרת]";

        string? compareName = null;
        if (!string.IsNullOrWhiteSpace(rule.CompareField))
            compareName = catalog.TryResolvePath(entity.Key, rule.CompareField, out var other, out _)
                ? other!.Field.DisplayName
                : "[שדה לא מוכר]";

        var name = field!.Field.DisplayName;
        var expectation = Expectation(rule, field.Field, compareName);
        return rule.Operator switch
        {
            OperatorCatalog.IsTrue or OperatorCatalog.IsFalse or OperatorCatalog.IsNotEmpty => $"{name}: {expectation}",
            OperatorCatalog.IsEmpty => $"ללא {name}",   // gender neutral: "ללא תעודת זהות"
            _ => $"{name} {expectation}"
        };
    }

    /// <summary>
    /// The "what is expected" part of a rule, without the field name: "קטן מ־18", "ריק", "מוקדם מתאריך תחילת העסקה".
    /// Shared by the definition description and by the per-record reason builder.
    /// </summary>
    /// <param name="compareText">Text for the compared field (name, or name + value) when this is a field comparison.</param>
    public static string Expectation(ConditionRule rule, FieldMeta field, string? compareText)
    {
        var op = OperatorCatalog.Find(rule.Operator)!;
        var label = op.LabelFor(field.DataType);

        return op.Arity switch
        {
            ValueArity.None => op.Key switch
            {
                OperatorCatalog.IsTrue => "כן",
                OperatorCatalog.IsFalse => "לא",
                OperatorCatalog.IsEmpty => "ריק",
                OperatorCatalog.IsNotEmpty => "מולא",
                _ => label
            },
            ValueArity.Single when compareText is not null => HebrewText.Attach(label, compareText),
            ValueArity.Single => HebrewText.Attach(label, Format(rule.Value, field)),
            ValueArity.Range => HebrewText.Attach($"בין {Format(rule.Value, field)} ל", Format(rule.ValueTo, field)),
            ValueArity.List => $"{label}: {string.Join(", ", (rule.Values ?? []).Select(v => Format(v, field)))}",
            _ => label
        };
    }

    private static string Format(JsonElement? element, FieldMeta field)
    {
        if (element is null) return "?";
        return ValueConverter.TryConvert(element.Value, field.DataType, out var value, out _)
            ? HebrewText.FormatValue(value, field)
            : element.Value.ToString();
    }
}
