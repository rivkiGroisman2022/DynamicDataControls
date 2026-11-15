using Dapper;
using DataControls.Core.Abstractions;
using DataControls.Core.Metadata;
using DataControls.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace DataControls.Infrastructure.Metadata;

/// <summary>
/// Loads the semantic layer from the meta.* tables and keeps it in memory.
/// Metadata rarely changes, so there is no reason to read it from the database on every request.
/// </summary>
public sealed class SqlMetadataProvider(
    SqlConnectionFactory connections,
    IMemoryCache cache,
    IOptions<DatabaseOptions> options) : IMetadataProvider
{
    private const string CacheKey = "metadata-catalog";

    public async Task<MetadataCatalog> GetCatalogAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out MetadataCatalog? cached) && cached is not null)
            return cached;

        var catalog = await LoadAsync(ct);
        cache.Set(CacheKey, catalog, TimeSpan.FromMinutes(options.Value.MetadataCacheMinutes));
        return catalog;
    }

    public void Invalidate() => cache.Remove(CacheKey);

    private async Task<MetadataCatalog> LoadAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT EntityId, EntityKey, DisplayName, DisplayNamePlural, Description, SchemaName, TableName,
                   PrimaryKeyColumn, TitleFieldKeys, Synonyms, IsControlTarget
            FROM meta.Entities;

            SELECT FieldId, EntityId, FieldKey, DisplayName, Description, ColumnName, DataType, Unit,
                   Synonyms, ShowInResults, SortOrder
            FROM meta.Fields;

            SELECT r.FromEntityId, r.NavigationKey, r.DisplayName, r.FromColumn, r.ToColumn, t.EntityKey AS TargetEntityKey
            FROM meta.Relations r
            JOIN meta.Entities t ON t.EntityId = r.ToEntityId;

            SELECT FieldId, Value, Label FROM meta.FieldOptions ORDER BY SortOrder;
            """;

        await using var connection = await connections.OpenAsync(ct);
        using var grid = await connection.QueryMultipleAsync(new CommandDefinition(sql, cancellationToken: ct));

        var entities = (await grid.ReadAsync<EntityRow>()).ToList();
        var fields = (await grid.ReadAsync<FieldRow>()).ToList();
        var relations = (await grid.ReadAsync<RelationRow>()).ToList();
        var options = (await grid.ReadAsync<OptionRow>()).ToLookup(o => o.FieldId);

        return new MetadataCatalog(entities.Select(e => new EntityMeta(
            e.EntityKey,
            e.DisplayName,
            e.DisplayNamePlural,
            e.Description,
            e.SchemaName,
            e.TableName,
            e.PrimaryKeyColumn,
            Split(e.TitleFieldKeys),
            Split(e.Synonyms),
            e.IsControlTarget,
            fields.Where(f => f.EntityId == e.EntityId)
                .OrderBy(f => f.SortOrder)
                .Select(f => new FieldMeta(
                    f.FieldKey, f.DisplayName, f.Description, f.ColumnName, ParseType(f.DataType), f.Unit,
                    Split(f.Synonyms), f.ShowInResults, f.SortOrder,
                    options[f.FieldId].Select(o => new FieldOption(o.Value, o.Label)).ToList()))
                .ToList(),
            relations.Where(r => r.FromEntityId == e.EntityId)
                .Select(r => new RelationMeta(r.NavigationKey, r.DisplayName, r.TargetEntityKey, r.FromColumn, r.ToColumn))
                .ToList())));
    }

    private static IReadOnlyList<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv) ? [] : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static FieldDataType ParseType(string type) => type switch
    {
        "string" => FieldDataType.String,
        "number" => FieldDataType.Number,
        "date" => FieldDataType.Date,
        "boolean" => FieldDataType.Boolean,
        _ => throw new Core.Exceptions.MetadataConfigurationException($"סוג נתון לא מוכר במטא-דאטה: {type}")
    };

    private sealed record EntityRow(int EntityId, string EntityKey, string DisplayName, string DisplayNamePlural,
        string? Description, string SchemaName, string TableName, string PrimaryKeyColumn, string? TitleFieldKeys,
        string? Synonyms, bool IsControlTarget);

    private sealed record FieldRow(int FieldId, int EntityId, string FieldKey, string DisplayName, string? Description,
        string ColumnName, string DataType, string? Unit, string? Synonyms, bool ShowInResults, int SortOrder);

    private sealed record RelationRow(int FromEntityId, string NavigationKey, string DisplayName, string FromColumn,
        string ToColumn, string TargetEntityKey);

    private sealed record OptionRow(int FieldId, string Value, string Label);
}
