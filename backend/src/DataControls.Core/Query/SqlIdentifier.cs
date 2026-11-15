using System.Text.RegularExpressions;
using DataControls.Core.Exceptions;

namespace DataControls.Core.Query;

/// <summary>
/// Handles table and column names. They cannot be sent as parameters (SQL does not allow it),
/// so two safeguards apply:
/// 1. Names only ever come from the metadata layer, never from user input.
/// 2. Even so, the name must match a strict pattern and is wrapped in [ ].
/// </summary>
public static partial class SqlIdentifier
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex ValidIdentifier();

    public static string Quote(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || !ValidIdentifier().IsMatch(name))
            throw new MetadataConfigurationException($"שם האובייקט '{name}' בהגדרות המטא-דאטה אינו חוקי.");

        return $"[{name}]";
    }

    public static string QuoteTable(string schema, string table) => $"{Quote(schema)}.{Quote(table)}";
}
