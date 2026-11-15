using DataControls.Core.Operators;
using DataControls.Core.Query;

namespace DataControls.Core.Describing;

/// <summary>
/// Explains to the user WHY a specific record failed the control.
/// Relies on the probe columns (__r0, __r1...) the query returns: each is 1 if that specific rule matched the row.
/// So for an OR control you can see exactly which of the conditions caused the failure, with the actual value:
/// "גיל: 16 (קטן מ־18)" · "כתובת אימייל: ריק".
/// </summary>
public static class ViolationReasonBuilder
{
    public static IReadOnlyList<string> Build(BuiltQuery query, IReadOnlyDictionary<string, object?> row) =>
        BuildWithRules(query, row).Select(r => r.Reason).ToList();

    /// <summary>Reasons together with the rule index, so the UI can filter results by "which condition failed".</summary>
    public static IReadOnlyList<(int RuleIndex, string Reason)> BuildWithRules(BuiltQuery query, IReadOnlyDictionary<string, object?> row)
    {
        var reasons = new List<(int, string)>();

        foreach (var probe in query.Rules)
        {
            // A rule under NOT "helps" the violation precisely when it does NOT match.
            var matched = row.TryGetValue(probe.FlagAlias, out var flag) && flag is not null && HebrewText.ToBool(flag);
            if (matched == probe.IsUnderNegation) continue;

            reasons.Add((probe.Index, Describe(query, probe, row)));
        }

        return reasons;
    }

    private static string Describe(BuiltQuery query, RuleProbe probe, IReadOnlyDictionary<string, object?> row)
    {
        var field = probe.Field.Field;
        var actual = HebrewText.FormatValue(ValueOf(query, probe.Field.Path, row), field);
        var prefix = probe.IsUnderNegation ? "לא מתקיים: " : "";

        // For these the actual value says it all ("כתובת אימייל: ריק", "מחלקה פעילה: לא", "סטטוס: פעיל").
        if (probe.Rule.Operator is OperatorCatalog.IsEmpty or OperatorCatalog.IsTrue or OperatorCatalog.IsFalse
                or OperatorCatalog.EqualsOp
            && probe.CompareField is null
            && !probe.IsUnderNegation)
            return $"{field.DisplayName}: {actual}";

        string? compareText = null;
        if (probe.CompareField is { } other)
            compareText = $"{other.Field.DisplayName}: {HebrewText.FormatValue(ValueOf(query, other.Path, row), other.Field)}";

        var expectation = DefinitionDescriber.Expectation(probe.Rule, field, compareText);
        return $"{prefix}{field.DisplayName}: {actual} ({expectation})";
    }

    private static object? ValueOf(BuiltQuery query, string path, IReadOnlyDictionary<string, object?> row)
    {
        var column = query.Columns.FirstOrDefault(c => string.Equals(c.Path, path, StringComparison.OrdinalIgnoreCase));
        return column is not null && row.TryGetValue(column.Alias, out var value) ? value : null;
    }
}
