using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace DataControls.Infrastructure.Data;

public sealed class DatabaseOptions
{
    public const string Section = "Database";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>The restricted database user that control queries run as (EXECUTE AS USER).</summary>
    public string ControlRunnerUser { get; set; } = "dc_control_runner";

    /// <summary>Maximum time for one control query. Protects the server from "heavy" controls.</summary>
    public int ControlQueryTimeoutSeconds { get; set; } = 30;

    public int MetadataCacheMinutes { get; set; } = 10;
}

public sealed class SqlConnectionFactory(IOptions<DatabaseOptions> options)
{
    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(options.Value.ConnectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
