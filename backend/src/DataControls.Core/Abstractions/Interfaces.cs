using DataControls.Core.Controls;
using DataControls.Core.Metadata;
using DataControls.Core.Query;

namespace DataControls.Core.Abstractions;

// Core defines WHAT it needs; Infrastructure decides HOW (SQL Server, Dapper...).
// This is Dependency Inversion: the business logic does not depend on the database and can be tested without one.

/// <summary>Supplies the semantic layer (with caching, because it rarely changes).</summary>
public interface IMetadataProvider
{
    Task<MetadataCatalog> GetCatalogAsync(CancellationToken ct = default);
    void Invalidate();
}

public interface IControlRepository
{
    Task<IReadOnlyList<ControlSummary>> GetAllAsync(CancellationToken ct = default);
    Task<Control?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken ct = default);
    Task<int> CreateAsync(ControlWriteModel control, CancellationToken ct = default);
    Task<bool> UpdateAsync(int id, ControlWriteModel control, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public interface IControlRunRepository
{
    Task AddAsync(ControlRunRecord run, CancellationToken ct = default);
    Task<IReadOnlyList<ControlRunHistoryItem>> GetRecentAsync(int controlId, int take, CancellationToken ct = default);
}

/// <summary>Runs a query that has already been built. Knows nothing about controls, only "run this SELECT safely".</summary>
public interface IControlQueryExecutor
{
    Task<IReadOnlyList<IReadOnlyDictionary<string, object?>>> QueryAsync(BuiltQuery query, CancellationToken ct = default);
}
