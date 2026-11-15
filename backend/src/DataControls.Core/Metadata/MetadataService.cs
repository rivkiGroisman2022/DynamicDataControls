using DataControls.Core.Abstractions;
using DataControls.Core.Operators;

namespace DataControls.Core.Metadata;

public sealed record EntityDto(string Key, string DisplayName, string DisplayNamePlural, string? Description, bool IsControlTarget);

public sealed record OperatorDto(string Key, string Label, string? DateLabel, IReadOnlyList<string> DataTypes, string Arity, bool SupportsFieldComparison);

/// <summary>A field the user can pick in the control editor, with everything the UI needs in order to show it in business terms.</summary>
public sealed record SelectableFieldDto(
    string Path,
    string DisplayName,
    string GroupLabel,
    string? Description,
    string DataType,
    string? Unit,
    int Depth,
    IReadOnlyList<FieldOption> Options,
    IReadOnlyList<string> Operators);

/// <summary>Exposes the semantic layer to the UI. This is what lets the UI be 100% generic.</summary>
public sealed class MetadataService(IMetadataProvider metadata)
{
    public async Task<IReadOnlyList<EntityDto>> GetEntitiesAsync(CancellationToken ct = default)
    {
        var catalog = await metadata.GetCatalogAsync(ct);
        return catalog.Entities
            .OrderBy(e => e.DisplayName)
            .Select(e => new EntityDto(e.Key, e.DisplayName, e.DisplayNamePlural, e.Description, e.IsControlTarget))
            .ToList();
    }

    public async Task<IReadOnlyList<SelectableFieldDto>> GetSelectableFieldsAsync(string entityKey, CancellationToken ct = default)
    {
        var catalog = await metadata.GetCatalogAsync(ct);
        var root = catalog.GetEntity(entityKey);

        return catalog.GetReachableFields(root.Key)
            .Select(f => new SelectableFieldDto(
                f.Path,
                f.Field.DisplayName,
                f.GroupLabel(root),
                f.Field.Description,
                f.Field.DataType.ToString().ToLowerInvariant(),
                f.Field.Unit,
                f.Steps.Count,
                f.Field.Options,
                OperatorCatalog.ForType(f.Field.DataType).Select(o => o.Key).ToList()))
            .ToList();
    }

    public static IReadOnlyList<OperatorDto> GetOperators() =>
        OperatorCatalog.Operators
            .Select(o => new OperatorDto(o.Key, o.Label, o.DateLabel,
                o.DataTypes.Select(t => t.ToString().ToLowerInvariant()).ToList(),
                o.Arity.ToString().ToLowerInvariant(),
                o.SupportsFieldComparison))
            .ToList();

    public void Refresh() => metadata.Invalidate();
}
