using System.Globalization;
using System.Text.Json;
using DataControls.Core.Metadata;

namespace DataControls.Core.Validation;

/// <summary>
/// Converts a JSON value from the definition into a typed .NET value that matches the field type.
/// Both the validator (to report errors) and the query builder (to create parameters) use it,
/// so "what counts as a valid value" is defined in exactly one place.
/// </summary>
public static class ValueConverter
{
    /// <summary>Numbers are sent as DECIMAL(38,10), whose integer part holds up to 28 digits; this keeps a wide safety margin.</summary>
    private const decimal MaxNumber = 1_000_000_000_000_000_000m;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "yyyy-MM-ddTHH:mm:ssZ", "o", "dd/MM/yyyy"];

    public static bool TryConvert(JsonElement element, FieldDataType type, out object? value, out string? error)
    {
        value = null;
        error = null;

        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            error = "לא הוזן ערך.";
            return false;
        }

        switch (type)
        {
            case FieldDataType.Number:
                var parsed = element.ValueKind switch
                {
                    JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
                    JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Number,
                        CultureInfo.InvariantCulture, out var d) => d,
                    _ => (decimal?)null
                };
                if (parsed is { } number && Math.Abs(number) < MaxNumber)
                {
                    value = number;
                    return true;
                }
                // A valid number that is simply too large (e.g. 1e40) gets an accurate message, not "not a number".
                var isNumeric = parsed is not null || element.ValueKind == JsonValueKind.Number ||
                    double.TryParse(Raw(element), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                error = isNumeric
                    ? $"הערך '{Raw(element)}' גדול מדי (מחוץ לטווח הנתמך)."
                    : $"הערך '{Raw(element)}' אינו מספר.";
                return false;

            case FieldDataType.Date:
                if (element.ValueKind == JsonValueKind.String &&
                    DateTime.TryParseExact(element.GetString(), DateFormats, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var date))
                {
                    value = date.Date;
                    return true;
                }
                error = $"הערך '{Raw(element)}' אינו תאריך תקין (נדרש yyyy-MM-dd).";
                return false;

            case FieldDataType.Boolean:
                if (element.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    value = element.GetBoolean();
                    return true;
                }
                if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var flag))
                {
                    value = flag;
                    return true;
                }
                error = $"הערך '{Raw(element)}' אינו כן/לא.";
                return false;

            case FieldDataType.String:
                if (element.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                {
                    var text = element.ValueKind == JsonValueKind.String ? element.GetString()! : element.GetRawText();
                    // An empty value would make "contains" match every record; "missing" is what isEmpty is for.
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        error = "לא הוזן ערך.";
                        return false;
                    }
                    if (text.Length > 4000)
                    {
                        error = "הערך ארוך מדי (מקסימום 4000 תווים).";
                        return false;
                    }
                    value = text;
                    return true;
                }
                error = $"הערך '{Raw(element)}' אינו טקסט.";
                return false;

            default:
                error = "סוג שדה לא נתמך.";
                return false;
        }
    }

    public static object Convert(JsonElement element, FieldDataType type) =>
        TryConvert(element, type, out var value, out var error)
            ? value!
            : throw new InvalidOperationException(error);

    private static string Raw(JsonElement element) => element.ValueKind == JsonValueKind.String
        ? element.GetString() ?? string.Empty
        : element.GetRawText();
}
