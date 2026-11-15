using DataControls.Core.Definitions;
using DataControls.Core.Metadata;
using DataControls.Core.Operators;

namespace DataControls.Core.Validation;

/// <param name="Path">Location of the error in the definition (e.g. "condition.conditions[1]"), so the UI can mark it.</param>
/// <param name="Message">Message for the business user.</param>
public sealed record ValidationError(string Path, string Message);

public sealed record ValidationResult(IReadOnlyList<ValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

/// <summary>
/// Checks a control definition against the metadata layer, before saving and again before every run.
/// Collects ALL errors (not just the first) so the user can fix everything at once.
/// This is also the gate for AI output: an AI suggestion goes through exactly the same checks.
/// </summary>
public sealed class ControlDefinitionValidator
{
    public const int MaxDepth = 5;
    public const int MaxRules = 50;
    public const int MaxListValues = 100;

    public ValidationResult Validate(ControlDefinition? definition, MetadataCatalog catalog)
    {
        var errors = new List<ValidationError>();

        if (definition is null)
        {
            errors.Add(new("", "לא התקבלה הגדרת בקרה."));
            return new(errors);
        }

        if (definition.Version != 1)
            errors.Add(new("version", $"גרסת הגדרה {definition.Version} אינה נתמכת."));

        var entity = catalog.FindEntity(definition.Entity);
        if (entity is null)
        {
            errors.Add(new("entity", string.IsNullOrWhiteSpace(definition.Entity)
                ? "לא נבחרו הנתונים שעליהם תרוץ הבקרה."
                : $"הישות '{definition.Entity}' אינה מוגדרת במערכת."));
            return new(errors);
        }

        if (!entity.IsControlTarget)
            errors.Add(new("entity", $"לא ניתן להגדיר בקרות על {entity.DisplayNamePlural}."));

        if (definition.Condition is null)
        {
            errors.Add(new("condition", "לא הוגדרו תנאים."));
            return new(errors);
        }

        var ruleCount = 0;
        ValidateNode(definition.Condition, "condition", depth: 1);

        if (ruleCount > MaxRules)
            errors.Add(new("condition", $"הבקרה מכילה {ruleCount} תנאים. המקסימום הוא {MaxRules}."));

        return new(errors);

        void ValidateNode(ConditionNode node, string path, int depth)
        {
            switch (node)
            {
                case ConditionGroup group:
                    if (depth > MaxDepth)
                    {
                        errors.Add(new(path, $"קינון הקבוצות עמוק מדי (מקסימום {MaxDepth} רמות)."));
                        return;
                    }
                    if (!LogicalOperators.IsValid(group.Logic))
                        errors.Add(new(path, $"קשר לוגי '{group.Logic}' אינו מוכר (נדרש and / or)."));
                    if (group.Conditions is null || group.Conditions.Count == 0)
                    {
                        errors.Add(new(path, "קבוצת תנאים ריקה. יש להוסיף לפחות תנאי אחד."));
                        return;
                    }
                    for (var i = 0; i < group.Conditions.Count; i++)
                        ValidateNode(group.Conditions[i], $"{path}.conditions[{i}]", depth + 1);
                    break;

                case ConditionRule rule:
                    ruleCount++;
                    ValidateRule(rule, path);
                    break;

                default:
                    errors.Add(new(path, "סוג תנאי לא מוכר."));
                    break;
            }
        }

        void ValidateRule(ConditionRule rule, string path)
        {
            if (!catalog.TryResolvePath(entity.Key, rule.Field, out var field, out var fieldError))
            {
                errors.Add(new($"{path}.field", fieldError!));
                return;
            }

            var dataType = field!.Field.DataType;
            var op = OperatorCatalog.Find(rule.Operator);
            if (op is null)
            {
                errors.Add(new($"{path}.operator", $"פעולת ההשוואה '{rule.Operator}' אינה מוכרת."));
                return;
            }

            if (!op.AppliesTo(dataType))
            {
                errors.Add(new($"{path}.operator",
                    $"לא ניתן להשתמש ב'{op.LabelFor(dataType)}' על השדה '{field.Field.DisplayName}'."));
                return;
            }

            switch (op.Arity)
            {
                case ValueArity.None:
                    break;

                case ValueArity.Single when !string.IsNullOrWhiteSpace(rule.CompareField):
                    if (!op.SupportsFieldComparison)
                    {
                        errors.Add(new($"{path}.compareField", $"'{op.Label}' אינו תומך בהשוואה לשדה אחר."));
                        break;
                    }
                    if (!catalog.TryResolvePath(entity.Key, rule.CompareField, out var other, out var otherError))
                    {
                        errors.Add(new($"{path}.compareField", otherError!));
                        break;
                    }
                    if (other!.Field.DataType != dataType)
                        errors.Add(new($"{path}.compareField",
                            $"לא ניתן להשוות את '{field.Field.DisplayName}' ל'{other.Field.DisplayName}' - סוגי נתונים שונים."));
                    else if (other.Path == field.Path)
                        errors.Add(new($"{path}.compareField", "לא ניתן להשוות שדה לעצמו."));
                    break;

                case ValueArity.Single:
                    CheckValue(rule.Value, $"{path}.value", out _);
                    break;

                case ValueArity.Range:
                    var fromOk = CheckValue(rule.Value, $"{path}.value", out var from);
                    var toOk = CheckValue(rule.ValueTo, $"{path}.valueTo", out var to);
                    if (fromOk && toOk && from is IComparable a && to is not null && a.CompareTo(to) > 0)
                        errors.Add(new($"{path}.valueTo", "ערך ה'עד' קטן מערך ה'מ'."));
                    break;

                case ValueArity.List:
                    if (rule.Values is null || rule.Values.Count == 0)
                    {
                        errors.Add(new($"{path}.values", "יש לבחור לפחות ערך אחד."));
                        break;
                    }
                    if (rule.Values.Count > MaxListValues)
                    {
                        errors.Add(new($"{path}.values", $"ניתן לבחור עד {MaxListValues} ערכים."));
                        break;
                    }
                    for (var i = 0; i < rule.Values.Count; i++)
                        CheckValue(rule.Values[i], $"{path}.values[{i}]", out _);
                    break;
            }

            bool CheckValue(System.Text.Json.JsonElement? element, string valuePath, out object? converted)
            {
                converted = null;
                if (element is null)
                {
                    errors.Add(new(valuePath, $"יש להזין ערך עבור '{field.Field.DisplayName}'."));
                    return false;
                }
                if (!ValueConverter.TryConvert(element.Value, dataType, out converted, out var valueError))
                {
                    errors.Add(new(valuePath, $"{field.Field.DisplayName}: {valueError}"));
                    return false;
                }
                // A field with a closed list of values (e.g. project status): an exact comparison must use one of them,
                // otherwise the control silently finds nothing. Text operators (contains...) stay free.
                var options = field.Field.Options;
                var text = converted as string;
                if (options.Count > 0
                    && op.Key is OperatorCatalog.EqualsOp or OperatorCatalog.NotEquals or OperatorCatalog.In or OperatorCatalog.NotIn
                    && !options.Any(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase)))
                {
                    errors.Add(new(valuePath,
                        $"{field.Field.DisplayName}: הערך '{text}' אינו אחד מהערכים האפשריים ({string.Join(", ", options.Select(o => o.Label))})."));
                    return false;
                }
                return true;
            }
        }
    }
}
