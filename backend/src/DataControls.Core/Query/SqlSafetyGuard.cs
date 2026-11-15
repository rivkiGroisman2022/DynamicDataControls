using System.Text.RegularExpressions;

namespace DataControls.Core.Query;

/// <summary>
/// Last check before execution (Defense in Depth). In theory the builder only produces safe SELECTs.
/// This check makes sure it stays that way even if a bug is introduced in the future.
/// It is one of three layers: (1) parameters + metadata names, (2) this check, (3) a read-only DB user.
/// </summary>
public static partial class SqlSafetyGuard
{
    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|DENY|INTO|OPENROWSET|OPENQUERY|OPENDATASOURCE|BULK|SHUTDOWN|WAITFOR|BACKUP|RESTORE|DBCC|USE|DECLARE|SP_\w*|XP_\w*)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ForbiddenKeyword();

    // Identifiers in brackets ([Name]) are stripped before the check, so a column like [UpdatedAt] does not fail it.
    [GeneratedRegex(@"\[[^\]]*\]")]
    private static partial Regex BracketedIdentifier();

    public static void EnsureSafe(string sql)
    {
        var problem = FindProblem(sql);
        if (problem is not null)
            throw new InvalidOperationException($"Generated SQL rejected by safety guard: {problem}");
    }

    public static string? FindProblem(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return "empty statement";
        if (!sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase)) return "must start with SELECT";

        var stripped = BracketedIdentifier().Replace(sql, "[]");
        if (stripped.Contains(';')) return "multiple statements";
        if (stripped.Contains("--") || stripped.Contains("/*")) return "comments are not allowed";
        if (stripped.Contains('\'')) return "string literals are not allowed (values must be parameters)";

        var match = ForbiddenKeyword().Match(stripped);
        return match.Success ? $"forbidden keyword '{match.Value}'" : null;
    }
}
