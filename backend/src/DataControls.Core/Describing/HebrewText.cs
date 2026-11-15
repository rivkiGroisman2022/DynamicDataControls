using System.Globalization;
using DataControls.Core.Metadata;

namespace DataControls.Core.Describing;

/// <summary>Formats values and attaches Hebrew prepositions ("קטן מ־18", "מוקדם מתאריך ההתחלה").</summary>
public static class HebrewText
{
    // Number format: thousands separators, but a plain minus sign (he-IL adds an invisible direction mark that breaks copy/search).
    private static readonly NumberFormatInfo Numbers = new() { NumberGroupSeparator = ",", NumberDecimalSeparator = ".", NegativeSign = "-" };
    private const char Maqaf = '־';

    /// <summary>
    /// Joins an operator label to the value. If the label ends with a one-letter preposition (מ, ל, ב),
    /// it attaches directly to a Hebrew word ("מתאריך") or with a maqaf to a number ("מ־18").
    /// </summary>
    public static string Attach(string label, string text)
    {
        var lastSpace = label.LastIndexOf(' ');
        var lastWord = lastSpace >= 0 ? label[(lastSpace + 1)..] : label;
        var isPreposition = lastWord is "מ" or "ל" or "ב";

        if (!isPreposition) return $"{label} {text}";
        if (text.Length > 0 && IsHebrewLetter(text[0])) return label + text;
        return label + Maqaf + text;
    }

    public static string FormatValue(object? value, FieldMeta field)
    {
        if (value is null or DBNull) return "ריק";

        switch (field.DataType)
        {
            case FieldDataType.Boolean:
                return ToBool(value) ? "כן" : "לא";

            case FieldDataType.Date:
                return value switch
                {
                    DateTime dt => dt.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    DateOnly d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                    _ => value.ToString() ?? ""
                };

            case FieldDataType.Number:
                var number = System.Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                var formatted = number.ToString("#,##0.##", Numbers);
                return field.Unit is null ? formatted : $"{formatted} {field.Unit}";

            default:
                var text = value.ToString() ?? "";
                var option = field.Options.FirstOrDefault(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase));
                return $"\"{option?.Label ?? text}\"";
        }
    }

    public static bool ToBool(object value) => value switch
    {
        bool b => b,
        byte b => b != 0,
        int i => i != 0,
        long l => l != 0,
        string s => s is "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase),
        _ => System.Convert.ToBoolean(value, CultureInfo.InvariantCulture)
    };

    private static bool IsHebrewLetter(char c) => c is >= 'א' and <= 'ת';
}
