namespace DataControls.Core.Metadata;

public enum FieldDataType
{
    String,
    Number,
    Date,
    Boolean
}

public sealed record FieldOption(string Value, string Label);

/// <summary>A business field, e.g. "שכר חודשי", mapped to a physical column.</summary>
public sealed record FieldMeta(
    string Key,
    string DisplayName,
    string? Description,
    string ColumnName,
    FieldDataType DataType,
    string? Unit,
    IReadOnlyList<string> Synonyms,
    bool ShowInResults,
    int SortOrder,
    IReadOnlyList<FieldOption> Options);

/// <summary>A many-to-one relation (lookup), e.g. Employee → Department.</summary>
public sealed record RelationMeta(
    string NavigationKey,
    string DisplayName,
    string TargetEntityKey,
    string FromColumn,
    string ToColumn);

/// <summary>A business entity, e.g. "עובד", mapped to a physical table.</summary>
public sealed record EntityMeta(
    string Key,
    string DisplayName,
    string DisplayNamePlural,
    string? Description,
    string SchemaName,
    string TableName,
    string PrimaryKeyColumn,
    IReadOnlyList<string> TitleFieldKeys,
    IReadOnlyList<string> Synonyms,
    bool IsControlTarget,
    IReadOnlyList<FieldMeta> Fields,
    IReadOnlyList<RelationMeta> Relations)
{
    public FieldMeta? FindField(string key) =>
        Fields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));

    public RelationMeta? FindRelation(string navigationKey) =>
        Relations.FirstOrDefault(r => string.Equals(r.NavigationKey, navigationKey, StringComparison.OrdinalIgnoreCase));
}

/// <summary>One step along a relation path.</summary>
public sealed record RelationStep(RelationMeta Relation, EntityMeta From, EntityMeta To);

/// <summary>A field path resolved against the metadata: which field, on which entity, through which relations.</summary>
public sealed record ResolvedField(string Path, EntityMeta Entity, FieldMeta Field, IReadOnlyList<RelationStep> Steps)
{
    /// <summary>The relation part of the path only ("department.branch"), or an empty string for the base entity.</summary>
    public string RelationPath => string.Join('.', Steps.Select(s => s.Relation.NavigationKey));

    /// <summary>Group label for the UI, e.g. "עובד › מחלקה › סניף".</summary>
    public string GroupLabel(EntityMeta baseEntity) =>
        string.Join(" › ", new[] { baseEntity.DisplayName }.Concat(Steps.Select(s => s.To.DisplayName)));
}
