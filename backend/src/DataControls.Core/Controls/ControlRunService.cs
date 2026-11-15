using System.Diagnostics;
using System.Globalization;
using DataControls.Core.Abstractions;
using DataControls.Core.Definitions;
using DataControls.Core.Describing;
using DataControls.Core.Exceptions;
using DataControls.Core.Metadata;
using DataControls.Core.Query;
using DataControls.Core.Validation;

namespace DataControls.Core.Controls;

/// <summary>
/// Running controls. The flow:
/// load definition → validate against current metadata → build SQL → safety guard → execute → build reasons → log history.
/// Each step is a separate component; this service only orchestrates them.
/// </summary>
public sealed class ControlRunService(
    IControlRepository controls,
    IControlRunRepository runs,
    IMetadataProvider metadata,
    IControlQueryExecutor executor,
    TimeProvider clock)
{
    private readonly ControlDefinitionValidator _validator = new();
    private readonly SqlQueryBuilder _builder = new();

    /// <summary>Runs a saved control and records the run in the history (success or failure).</summary>
    public async Task<ControlRunResultDto> RunAsync(int controlId, int maxRows = SqlQueryBuilder.DefaultMaxRows, CancellationToken ct = default)
    {
        var control = await controls.GetByIdAsync(controlId, ct)
                      ?? throw new NotFoundException($"בקרה מספר {controlId} לא נמצאה.");

        var startedAt = clock.GetLocalNow().DateTime;
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var result = await ExecuteAsync(control.Definition, control.Name, control.Id, maxRows, ct);
            await runs.AddAsync(new ControlRunRecord(control.Id, startedAt, (int)stopwatch.ElapsedMilliseconds,
                "success", result.TotalViolations, null), ct);
            return result;
        }
        catch (DataControlsException ex)
        {
            // The failure itself is recorded too: a control that "stopped working" (e.g. after metadata changed) shows up in the history.
            await runs.AddAsync(new ControlRunRecord(control.Id, startedAt, (int)stopwatch.ElapsedMilliseconds,
                "failed", null, Truncate(ex.Message, 2000)), CancellationToken.None);
            throw;
        }
    }

    /// <summary>Test run of an unsaved definition (from the editor or from an AI suggestion). Not recorded in the history.</summary>
    public Task<ControlRunResultDto> TestRunAsync(ControlDefinition definition, int maxRows = 200, CancellationToken ct = default) =>
        ExecuteAsync(definition, "הרצת ניסיון", null, maxRows, ct);

    public Task<IReadOnlyList<ControlRunHistoryItem>> GetHistoryAsync(int controlId, int take = 20, CancellationToken ct = default) =>
        runs.GetRecentAsync(controlId, Math.Clamp(take, 1, 100), ct);

    private async Task<ControlRunResultDto> ExecuteAsync(
        ControlDefinition definition, string name, int? controlId, int maxRows, CancellationToken ct)
    {
        var catalog = await metadata.GetCatalogAsync(ct);

        var validation = _validator.Validate(definition, catalog);
        if (!validation.IsValid)
            throw new DefinitionValidationException(validation.Errors);

        var query = _builder.Build(definition, catalog, maxRows);
        SqlSafetyGuard.EnsureSafe(query.Sql);

        var stopwatch = Stopwatch.StartNew();
        var rows = await executor.QueryAsync(query, ct);
        stopwatch.Stop();

        var total = rows.Count == 0 ? 0 : Convert.ToInt32(rows[0][BuiltQuery.TotalAlias], CultureInfo.InvariantCulture);

        return new ControlRunResultDto(
            controlId,
            name,
            query.Entity.Key,
            query.Entity.DisplayNamePlural,
            DefinitionDescriber.Describe(definition, catalog),
            total,
            rows.Count,
            total > rows.Count,
            stopwatch.ElapsedMilliseconds,
            clock.GetLocalNow().DateTime,
            query.Columns.Select(ToColumnDto).ToList(),
            query.Rules.Select(p => new RunRuleDto(p.Index,
                (p.IsUnderNegation ? "לא מתקיים: " : "") + DefinitionDescriber.DescribeRule(p.Rule, catalog, query.Entity))).ToList(),
            rows.Select(r => ToRowDto(query, r)).ToList());
    }

    private static RunColumnDto ToColumnDto(ResultColumn c) => new(
        c.Path,
        c.Field.Field.DisplayName,
        c.GroupLabel,
        c.Field.Field.DataType.ToString().ToLowerInvariant(),
        c.Field.Field.Unit,
        c.IsRuleField,
        c.Field.Field.Options);

    private static ViolationRowDto ToRowDto(BuiltQuery query, IReadOnlyDictionary<string, object?> row)
    {
        var values = query.Columns.ToDictionary(c => c.Path, c => row.GetValueOrDefault(c.Alias));
        var titleParts = query.Columns.Where(c => c.IsTitle)
            .Select(c => row.GetValueOrDefault(c.Alias)?.ToString())
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var id = row.GetValueOrDefault(BuiltQuery.IdAlias)?.ToString() ?? "";
        var title = string.Join(" ", titleParts);

        var reasons = ViolationReasonBuilder.BuildWithRules(query, row);
        return new ViolationRowDto(id, title.Length > 0 ? title : $"#{id}", values,
            reasons.Select(r => r.Reason).ToList(), reasons.Select(r => r.RuleIndex).ToList());
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
