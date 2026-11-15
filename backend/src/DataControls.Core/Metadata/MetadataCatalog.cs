using DataControls.Core.Exceptions;

namespace DataControls.Core.Metadata;

/// <summary>
/// An in-memory view of the whole semantic layer (meta.*).
/// It is the only component that knows how to turn a business path ("department.isActive")
/// into a field, an entity and a chain of relations.
/// </summary>
public sealed class MetadataCatalog
{
    /// <summary>Maximum relation depth (join count) for one path. Protects performance and keeps the UI simple.</summary>
    public const int MaxRelationDepth = 3;

    private readonly Dictionary<string, EntityMeta> _entities;

    public MetadataCatalog(IEnumerable<EntityMeta> entities)
    {
        _entities = entities.ToDictionary(e => e.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<EntityMeta> Entities => _entities.Values;

    public EntityMeta? FindEntity(string? key) =>
        key is not null && _entities.TryGetValue(key, out var entity) ? entity : null;

    public EntityMeta GetEntity(string key) =>
        FindEntity(key) ?? throw new NotFoundException($"הישות '{key}' אינה מוגדרת במערכת.");

    /// <summary>
    /// Resolves a field path from the base entity. Returns an error message instead of throwing,
    /// so the validator can collect every error in one pass.
    /// </summary>
    public bool TryResolvePath(string baseEntityKey, string? path, out ResolvedField? resolved, out string? error)
    {
        resolved = null;
        error = null;

        var current = FindEntity(baseEntityKey);
        if (current is null)
        {
            error = $"הישות '{baseEntityKey}' אינה מוגדרת.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "לא נבחר שדה.";
            return false;
        }

        var segments = path.Split('.', StringSplitOptions.TrimEntries);
        if (segments.Any(string.IsNullOrEmpty))
        {
            error = $"נתיב השדה '{path}' אינו תקין.";
            return false;
        }

        if (segments.Length - 1 > MaxRelationDepth)
        {
            error = $"נתיב השדה '{path}' עובר דרך יותר מ-{MaxRelationDepth} קשרים.";
            return false;
        }

        var steps = new List<RelationStep>();
        var canonical = new List<string>();

        // Every segment except the last is a relation; the last one is the field.
        foreach (var navigationKey in segments[..^1])
        {
            var relation = current.FindRelation(navigationKey);
            if (relation is null)
            {
                error = $"ל{current.DisplayName} אין קשר בשם '{navigationKey}'.";
                return false;
            }

            var target = FindEntity(relation.TargetEntityKey);
            if (target is null)
            {
                error = $"הקשר '{relation.DisplayName}' מצביע על ישות שאינה מוגדרת.";
                return false;
            }

            steps.Add(new RelationStep(relation, current, target));
            canonical.Add(relation.NavigationKey);
            current = target;
        }

        var field = current.FindField(segments[^1]);
        if (field is null)
        {
            error = $"ל{current.DisplayName} אין שדה בשם '{segments[^1]}'.";
            return false;
        }

        canonical.Add(field.Key);
        resolved = new ResolvedField(string.Join('.', canonical), current, field, steps);
        return true;
    }

    public ResolvedField ResolvePath(string baseEntityKey, string path) =>
        TryResolvePath(baseEntityKey, path, out var resolved, out var error)
            ? resolved!
            : throw new MetadataConfigurationException(error!);

    /// <summary>
    /// Every field the user can pick for a control on this entity: its own fields plus fields of
    /// related entities (up to <see cref="MaxRelationDepth"/>, without cycles). Drives the UI field picker.
    /// </summary>
    public IReadOnlyList<ResolvedField> GetReachableFields(string baseEntityKey)
    {
        var root = GetEntity(baseEntityKey);
        var result = new List<ResolvedField>();
        Walk(root, [], [root.Key]);
        return result;

        void Walk(EntityMeta entity, List<RelationStep> steps, HashSet<string> visited)
        {
            var prefix = string.Concat(steps.Select(s => s.Relation.NavigationKey + "."));
            foreach (var field in entity.Fields.OrderBy(f => f.SortOrder))
            {
                result.Add(new ResolvedField(prefix + field.Key, entity, field, steps.ToList()));
            }

            if (steps.Count >= MaxRelationDepth) return;

            foreach (var relation in entity.Relations)
            {
                var target = FindEntity(relation.TargetEntityKey);
                if (target is null || visited.Contains(target.Key)) continue;

                Walk(target,
                    [.. steps, new RelationStep(relation, entity, target)],
                    [.. visited, target.Key]);
            }
        }
    }
}
