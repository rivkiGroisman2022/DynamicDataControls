using System.Data;
using System.Text.RegularExpressions;
using DataControls.Core.Abstractions;
using DataControls.Core.Exceptions;
using DataControls.Core.Metadata;
using DataControls.Core.Query;
using DataControls.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DataControls.Infrastructure.Query;

/// <summary>
/// Runs a built control query against SQL Server.
/// - Runs under the read-only user (EXECUTE AS USER) - even a SELECT cannot touch the ctl tables.
/// - Typed parameters (SqlParameter with an explicit SqlDbType) - no SQL injection, and a stable execution plan.
/// - CommandTimeout - a heavy control does not hold up the server.
/// - Technical errors are written to the log in full, while the user gets a clear message without internal details.
/// </summary>
public sealed partial class SqlControlQueryExecutor(
    SqlConnectionFactory connections,
    IOptions<DatabaseOptions> options,
    ILogger<SqlControlQueryExecutor> logger) : IControlQueryExecutor
{
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex SafeUserName();

    public async Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(BuiltQuery query, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!SafeUserName().IsMatch(settings.ControlRunnerUser))
            throw new MetadataConfigurationException("שם משתמש ההרצה בהגדרות אינו חוקי.");

        try
        {
            await using var connection = await connections.OpenAsync(ct);

            // Switch the security context to the restricted user. Applies until REVERT or until the connection closes.
            await using (var impersonate = new SqlCommand($"EXECUTE AS USER = N'{settings.ControlRunnerUser}';", connection))
                await impersonate.ExecuteNonQueryAsync(ct);

            try
            {
                await using var command = new SqlCommand(query.Sql, connection)
                {
                    CommandType = CommandType.Text,
                    CommandTimeout = settings.ControlQueryTimeoutSeconds
                };
                foreach (var parameter in query.Parameters)
                    command.Parameters.Add(ToSqlParameter(parameter));

                var rows = new List<IReadOnlyDictionary<string, object?>>();
                await using var reader = await command.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    var row = new Dictionary<string, object?>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
                    for (var i = 0; i < reader.FieldCount; i++)
                        row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(row);
                }
                return rows;
            }
            finally
            {
                // Pooled connections are reset anyway (sp_reset_connection), but we revert explicitly.
                if (connection.State == ConnectionState.Open)
                {
                    await using var revert = new SqlCommand("REVERT;", connection);
                    await revert.ExecuteNonQueryAsync(CancellationToken.None);
                }
            }
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Control query failed (SQL error {Number}). SQL: {Sql}", ex.Number, query.Sql);
            throw new QueryExecutionException(ex.Number switch
            {
                -2 => $"הבקרה ארכה יותר מ-{settings.ControlQueryTimeoutSeconds} שניות ובוטלה. כדאי לצמצם את התנאים.",
                229 or 230 => "אין הרשאה לקרוא חלק מהנתונים שבבקרה. יש לפנות למנהל המערכת.",
                207 or 208 => "מבנה הנתונים השתנה ואינו תואם להגדרות המערכת. יש לפנות למנהל המערכת.",
                8115 => "אחד הערכים בבקרה גדול מדי עבור השדה.",
                _ => "אירעה שגיאה בהרצת הבקרה מול בסיס הנתונים."
            }, ex);
        }
    }

    private static SqlParameter ToSqlParameter(QueryParameter p)
    {
        if (p.Value is int intValue)
            return new SqlParameter(p.Name, SqlDbType.Int) { Value = intValue };

        return p.Type switch
        {
            FieldDataType.Number => new SqlParameter(p.Name, SqlDbType.Decimal) { Precision = 38, Scale = 10, Value = p.Value ?? DBNull.Value },
            FieldDataType.Date => new SqlParameter(p.Name, SqlDbType.Date) { Value = p.Value ?? DBNull.Value },
            FieldDataType.Boolean => new SqlParameter(p.Name, SqlDbType.Bit) { Value = p.Value ?? DBNull.Value },
            _ => new SqlParameter(p.Name, SqlDbType.NVarChar, 4000) { Value = p.Value ?? DBNull.Value }
        };
    }
}
