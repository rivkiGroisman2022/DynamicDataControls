using Dapper;
using DataControls.Core.Abstractions;
using DataControls.Core.Controls;
using DataControls.Infrastructure.Data;

namespace DataControls.Infrastructure.Controls;

public sealed class SqlControlRunRepository(SqlConnectionFactory connections) : IControlRunRepository
{
    public async Task AddAsync(ControlRunRecord run, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO ctl.ControlRuns (ControlId, StartedAt, DurationMs, Status, ViolationCount, ErrorMessage)
            VALUES (@ControlId, @StartedAt, @DurationMs, @Status, @ViolationCount, @ErrorMessage);
            """;
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(sql, run, cancellationToken: ct));
    }

    public async Task<IReadOnlyList<ControlRunHistoryItem>> GetRecentAsync(int controlId, int take, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@take) RunId, StartedAt, DurationMs, Status, ViolationCount, ErrorMessage
            FROM ctl.ControlRuns WHERE ControlId = @controlId
            ORDER BY StartedAt DESC;
            """;
        await using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<ControlRunHistoryItem>(
            new CommandDefinition(sql, new { controlId, take }, cancellationToken: ct));
        return rows.ToList();
    }
}
