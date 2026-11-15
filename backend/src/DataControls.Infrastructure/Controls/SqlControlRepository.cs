using Dapper;
using DataControls.Core.Abstractions;
using DataControls.Core.Controls;
using DataControls.Core.Definitions;
using DataControls.Core.Exceptions;
using DataControls.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace DataControls.Infrastructure.Controls;

/// <summary>Stores control definitions in ctl.Controls. Plain parameterized SQL through Dapper.</summary>
public sealed class SqlControlRepository(SqlConnectionFactory connections) : IControlRepository
{
    public async Task<IReadOnlyList<ControlSummary>> GetAllAsync(CancellationToken ct = default)
    {
        // OUTER APPLY: for each control, its last run (if any), in a single query.
        const string sql = """
            SELECT c.ControlId AS Id, c.Name, c.Description, c.EntityKey, c.DefinitionJson, c.Severity, c.Source,
                   c.IsActive, c.CreatedAt, c.UpdatedAt,
                   lr.StartedAt AS LastRunAt, lr.Status AS LastRunStatus, lr.ViolationCount AS LastViolationCount
            FROM ctl.Controls c
            OUTER APPLY (SELECT TOP (1) r.StartedAt, r.Status, r.ViolationCount
                         FROM ctl.ControlRuns r
                         WHERE r.ControlId = c.ControlId
                         ORDER BY r.StartedAt DESC) lr
            ORDER BY c.Name;
            """;

        await using var connection = await connections.OpenAsync(ct);
        var rows = await connection.QueryAsync<ControlSummary>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<Control?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT ControlId AS Id, Name, Description, EntityKey, DefinitionJson, Severity, Source, IsActive, CreatedAt, UpdatedAt
            FROM ctl.Controls WHERE ControlId = @id;
            """;

        await using var connection = await connections.OpenAsync(ct);
        var row = await connection.QuerySingleOrDefaultAsync<ControlRow>(new CommandDefinition(sql, new { id }, cancellationToken: ct));
        return row is null
            ? null
            : new Control(row.Id, row.Name, row.Description, row.EntityKey, DefinitionJson.Deserialize(row.DefinitionJson),
                row.Severity, row.Source, row.IsActive, row.CreatedAt, row.UpdatedAt);
    }

    public async Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct = default)
    {
        const string sql = "SELECT CASE WHEN EXISTS (SELECT 1 FROM ctl.Controls WHERE Name = @name AND (@excludeId IS NULL OR ControlId <> @excludeId)) THEN 1 ELSE 0 END;";
        await using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(sql, new { name, excludeId }, cancellationToken: ct));
    }

    public async Task<int> CreateAsync(ControlWriteModel control, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO ctl.Controls (Name, Description, EntityKey, DefinitionJson, Severity, Source, IsActive)
            OUTPUT INSERTED.ControlId
            VALUES (@Name, @Description, @EntityKey, @DefinitionJson, @Severity, @Source, @IsActive);
            """;

        await using var connection = await connections.OpenAsync(ct);
        return await WithUniqueNameHandling(() =>
            connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, control, cancellationToken: ct)), control.Name);
    }

    public async Task<bool> UpdateAsync(int id, ControlWriteModel control, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE ctl.Controls
            SET Name = @Name, Description = @Description, EntityKey = @EntityKey, DefinitionJson = @DefinitionJson,
                Severity = @Severity, Source = @Source, IsActive = @IsActive, UpdatedAt = SYSDATETIME()
            WHERE ControlId = @Id;
            """;

        await using var connection = await connections.OpenAsync(ct);
        var affected = await WithUniqueNameHandling(() => connection.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id, control.Name, control.Description, control.EntityKey, control.DefinitionJson,
            control.Severity, control.Source, control.IsActive
        }, cancellationToken: ct)), control.Name);
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM ctl.Controls WHERE ControlId = @id;", new { id }, cancellationToken: ct)) > 0;
    }

    /// <summary>
    /// Duplicate names are checked in the service, but two simultaneous requests could both pass that check.
    /// The UNIQUE constraint in the database is the final guarantee, and its error (2627/2601) is turned into a clear message.
    /// </summary>
    private static async Task<T> WithUniqueNameHandling<T>(Func<Task<T>> action, string name)
    {
        try { return await action(); }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new ConflictException($"כבר קיימת בקרה בשם '{name}'.");
        }
    }

    private sealed record ControlRow(int Id, string Name, string? Description, string EntityKey, string DefinitionJson,
        string Severity, string Source, bool IsActive, DateTime CreatedAt, DateTime? UpdatedAt);
}
