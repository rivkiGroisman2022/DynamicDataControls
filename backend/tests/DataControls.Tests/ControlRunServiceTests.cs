using DataControls.Core.Abstractions;
using DataControls.Core.Controls;
using DataControls.Core.Exceptions;
using DataControls.Core.Metadata;
using DataControls.Core.Query;
using static DataControls.Tests.TestData;

namespace DataControls.Tests;

/// <summary>
/// Tests the whole run flow with fakes in place of the database.
/// Possible precisely because Core depends only on interfaces (Dependency Inversion).
/// </summary>
public class ControlRunServiceTests
{
    private sealed class FakeMetadata : IMetadataProvider
    {
        public Task<MetadataCatalog> GetCatalogAsync(CancellationToken ct = default) => Task.FromResult(Catalog());
        public void Invalidate() { }
    }

    private sealed class FakeControls(Control? control) : IControlRepository
    {
        public Task<IReadOnlyList<ControlSummary>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Control?> GetByIdAsync(int id, CancellationToken ct = default) => Task.FromResult(control?.Id == id ? control : null);
        public Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct = default) => Task.FromResult(false);
        public Task<int> CreateAsync(ControlWriteModel c, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> UpdateAsync(int id, ControlWriteModel c, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> DeleteAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeRuns : IControlRunRepository
    {
        public List<ControlRunRecord> Saved { get; } = [];
        public Task AddAsync(ControlRunRecord run, CancellationToken ct = default) { Saved.Add(run); return Task.CompletedTask; }
        public Task<IReadOnlyList<ControlRunHistoryItem>> GetRecentAsync(int controlId, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ControlRunHistoryItem>>([]);
    }

    /// <summary>"Database" that returns one row: a 16-year-old employee earning 17,000.</summary>
    private sealed class FakeExecutor(bool fail = false) : IControlQueryExecutor
    {
        public BuiltQuery? LastQuery { get; private set; }

        public Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(BuiltQuery query, CancellationToken ct = default)
        {
            LastQuery = query;
            if (fail) throw new QueryExecutionException("timeout");

            var row = new Dictionary<string, object?> { ["__id"] = 12, ["__total"] = 1, ["__r0"] = 1, ["__r1"] = 1 };
            foreach (var c in query.Columns)
                row[c.Alias] = c.Path switch { "firstName" => "אלון", "lastName" => "חדד", "age" => 16, "salary" => 17000m, _ => null };
            return Task.FromResult<IReadOnlyList<IReadOnlyDictionary<string, object?>>>([row]);
        }
    }

    private static Control MinorsControl() => new(1, "קטינים עם שכר גבוה", null, "Employee",
        Def("Employee", And(Rule("age", "lessThan", 18), Rule("salary", "greaterThan", 15000))),
        "high", "manual", true, DateTime.Now, null);

    [Fact]
    public async Task Run_returns_violations_with_title_and_reasons_and_records_history()
    {
        var runs = new FakeRuns();
        var service = new ControlRunService(new FakeControls(MinorsControl()), runs, new FakeMetadata(), new FakeExecutor(), TimeProvider.System);

        var result = await service.RunAsync(1);

        Assert.Equal(1, result.TotalViolations);
        var row = Assert.Single(result.Rows);
        Assert.Equal("12", row.RecordId);
        Assert.Equal("אלון חדד", row.Title);
        Assert.Equal(["גיל: 16 (קטן מ־18)", "שכר חודשי: 17,000 ש\"ח (גדול מ־15,000 ש\"ח)"], row.Reasons);

        var history = Assert.Single(runs.Saved);
        Assert.Equal("success", history.Status);
        Assert.Equal(1, history.ViolationCount);
    }

    [Fact]
    public async Task Failed_run_is_recorded_and_error_is_rethrown()
    {
        var runs = new FakeRuns();
        var service = new ControlRunService(new FakeControls(MinorsControl()), runs, new FakeMetadata(), new FakeExecutor(fail: true), TimeProvider.System);

        await Assert.ThrowsAsync<QueryExecutionException>(() => service.RunAsync(1));

        Assert.Equal("failed", Assert.Single(runs.Saved).Status);
    }

    [Fact]
    public async Task Missing_control_gives_not_found()
    {
        var service = new ControlRunService(new FakeControls(null), new FakeRuns(), new FakeMetadata(), new FakeExecutor(), TimeProvider.System);

        await Assert.ThrowsAsync<NotFoundException>(() => service.RunAsync(99));
    }

    [Fact]
    public async Task Test_run_does_not_write_history()
    {
        var runs = new FakeRuns();
        var service = new ControlRunService(new FakeControls(null), runs, new FakeMetadata(), new FakeExecutor(), TimeProvider.System);

        await service.TestRunAsync(Def("Employee", Rule("age", "lessThan", 18)));

        Assert.Empty(runs.Saved);
    }
}
