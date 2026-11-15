using DataControls.Core.Metadata;

namespace DataControls.Core.Operators;

/// <summary>How many values the operator needs.</summary>
public enum ValueArity
{
    /// <summary>No value (isEmpty, isTrue...).</summary>
    None,
    /// <summary>One value, or a comparison with another field.</summary>
    Single,
    /// <summary>Two values: from-to (between).</summary>
    Range,
    /// <summary>A list of values (in).</summary>
    List
}

/// <param name="Key">Stable key stored in the definition.</param>
/// <param name="Label">Hebrew label for the UI and the descriptions.</param>
/// <param name="DateLabel">Alternative label for dates ("מוקדם מ" instead of "קטן מ").</param>
public sealed record OperatorInfo(
    string Key,
    string Label,
    string? DateLabel,
    IReadOnlyList<FieldDataType> DataTypes,
    ValueArity Arity,
    bool SupportsFieldComparison)
{
    public string LabelFor(FieldDataType type) =>
        type == FieldDataType.Date && DateLabel is not null ? DateLabel : Label;

    public bool AppliesTo(FieldDataType type) => DataTypes.Contains(type);
}

/// <summary>
/// The full list of supported operators. Everything about an operator is defined here once:
/// the UI gets the list from the API, the validator checks against it and the query builder translates it.
/// Adding an operator = one line here + one case in the query builder.
/// </summary>
public static class OperatorCatalog
{
    public const string EqualsOp = "equals";
    public const string NotEquals = "notEquals";
    public const string GreaterThan = "greaterThan";
    public const string GreaterOrEqual = "greaterOrEqual";
    public const string LessThan = "lessThan";
    public const string LessOrEqual = "lessOrEqual";
    public const string Between = "between";
    public const string In = "in";
    public const string NotIn = "notIn";
    public const string Contains = "contains";
    public const string NotContains = "notContains";
    public const string StartsWith = "startsWith";
    public const string EndsWith = "endsWith";
    public const string IsEmpty = "isEmpty";
    public const string IsNotEmpty = "isNotEmpty";
    public const string IsTrue = "isTrue";
    public const string IsFalse = "isFalse";
    public const string IsInPast = "isInPast";
    public const string IsInFuture = "isInFuture";

    private static readonly FieldDataType[] Comparable = [FieldDataType.Number, FieldDataType.Date];
    private static readonly FieldDataType[] Equatable = [FieldDataType.String, FieldDataType.Number, FieldDataType.Date];
    private static readonly FieldDataType[] Text = [FieldDataType.String];
    private static readonly FieldDataType[] All = Enum.GetValues<FieldDataType>();

    public static readonly IReadOnlyList<OperatorInfo> Operators =
    [
        new(EqualsOp,       "שווה ל",             null,        Equatable,                ValueArity.Single, true),
        new(NotEquals,      "שונה מ",             null,              Equatable,                ValueArity.Single, true),
        new(GreaterThan,    "גדול מ",             "מאוחר מ",         Comparable,               ValueArity.Single, true),
        new(GreaterOrEqual, "גדול או שווה ל",     "לא מוקדם מ",      Comparable,               ValueArity.Single, true),
        new(LessThan,       "קטן מ",              "מוקדם מ",         Comparable,               ValueArity.Single, true),
        new(LessOrEqual,    "קטן או שווה ל",      "לא מאוחר מ",      Comparable,               ValueArity.Single, true),
        new(Between,        "בין",                null,              Comparable,               ValueArity.Range,  false),
        new(In,             "אחד מהערכים",        null,              [FieldDataType.String, FieldDataType.Number], ValueArity.List, false),
        new(NotIn,          "אף אחד מהערכים",     null,              [FieldDataType.String, FieldDataType.Number], ValueArity.List, false),
        new(Contains,       "מכיל",               null,              Text,                     ValueArity.Single, false),
        new(NotContains,    "לא מכיל",            null,              Text,                     ValueArity.Single, false),
        new(StartsWith,     "מתחיל ב",            null,              Text,                     ValueArity.Single, false),
        new(EndsWith,       "מסתיים ב",           null,              Text,                     ValueArity.Single, false),
        new(IsEmpty,        "ריק / חסר",          null,              All,                      ValueArity.None,   false),
        new(IsNotEmpty,     "מולא (לא ריק)",      null,              All,                      ValueArity.None,   false),
        new(IsTrue,         "כן",                 null,              [FieldDataType.Boolean],  ValueArity.None,   false),
        new(IsFalse,        "לא",                 null,              [FieldDataType.Boolean],  ValueArity.None,   false),
        new(IsInPast,       "כבר עבר",            null,              [FieldDataType.Date],     ValueArity.None,   false),
        new(IsInFuture,     "עתידי",              null,              [FieldDataType.Date],     ValueArity.None,   false),
    ];

    private static readonly Dictionary<string, OperatorInfo> ByKey =
        Operators.ToDictionary(o => o.Key, StringComparer.OrdinalIgnoreCase);

    public static OperatorInfo? Find(string? key) =>
        key is not null && ByKey.TryGetValue(key, out var op) ? op : null;

    /// <summary>
    /// The operators that fit a type. Generic operators (that fit every type, like "empty") come last,
    /// so the default (first) is the most natural one for the field: "yes" for a yes/no field, "equals" for a number.
    /// </summary>
    public static IEnumerable<OperatorInfo> ForType(FieldDataType type) =>
        Operators.Where(o => o.AppliesTo(type)).OrderBy(o => o.DataTypes.Count == All.Length ? 1 : 0);
}
