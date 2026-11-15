using System.Text;
using DataControls.Core.Definitions;
using DataControls.Core.Metadata;
using DataControls.Core.Operators;
using DataControls.Core.Validation;

namespace DataControls.Core.Query;

/// <summary>
/// Translates a control definition (JSON) into a parameterized SQL query, using only metadata.
///
/// Example output for "age &lt; 18 and department is inactive":
/// <code>
/// SELECT TOP (@__maxRows)
///     t0.[EmployeeId] AS [__id], COUNT(*) OVER () AS [__total],
///     t0.[FirstName] AS [c0], ..., j1.[IsActive] AS [c9],
///     CASE WHEN (t0.[Age] &lt; @p0) THEN 1 ELSE 0 END AS [__r0], ...
/// FROM [dbo].[Employees] AS t0
/// LEFT JOIN [dbo].[Departments] AS j1 ON j1.[DepartmentId] = t0.[DepartmentId]
/// WHERE ((t0.[Age] &lt; @p0) AND (j1.[IsActive] = 0))
/// ORDER BY t0.[EmployeeId]
/// </code>
/// Principles:
/// - Values → parameters only (@p0, @p1...). User text is never written into the SQL.
/// - Names → only from metadata, through SqlIdentifier.Quote.
/// - LEFT JOIN (not INNER) so that a missing relation (employee without a department) is not lost and can be checked.
/// - Each relation path gets one JOIN, even if several rules use it.
/// - COUNT(*) OVER () returns the total number of violations even when only TOP N rows are returned.
/// </summary>
public sealed class SqlQueryBuilder
{
    public const int DefaultMaxRows = 1000;
    public const int AbsoluteMaxRows = 5000;
    private const string BaseAlias = "t0";

    private readonly ControlDefinitionValidator _validator = new();

    public BuiltQuery Build(ControlDefinition definition, MetadataCatalog catalog, int maxRows = DefaultMaxRows)
    {
        // The builder never trusts its caller: it validates again. Invalid input = programmer error.
        var validation = _validator.Validate(definition, catalog);
        if (!validation.IsValid)
            throw new Exceptions.DefinitionValidationException(validation.Errors);

        var context = new BuildContext(catalog, catalog.GetEntity(definition.Entity));
        var where = context.Translate(definition.Condition, underNegation: false);
        return context.Compose(where, Math.Clamp(maxRows, 1, AbsoluteMaxRows));
    }

    private sealed class BuildContext(MetadataCatalog catalog, EntityMeta root)
    {
        private readonly List<QueryParameter> _parameters = [];
        private readonly List<RuleProbe> _probes = [];
        private readonly List<string> _probeSql = [];

        // Relation path → table alias. "" = the base entity.
        private readonly Dictionary<string, string> _joinAliases = new(StringComparer.OrdinalIgnoreCase) { [""] = BaseAlias };
        private readonly List<string> _joins = [];

        private readonly List<ResultColumn> _columns = [];
        private readonly HashSet<string> _columnPaths = new(StringComparer.OrdinalIgnoreCase);

        public string Translate(ConditionNode node, bool underNegation) => node switch
        {
            ConditionGroup group => TranslateGroup(group, underNegation),
            ConditionRule rule => TranslateRule(rule, underNegation),
            _ => throw new InvalidOperationException("Unknown condition node.")
        };

        private string TranslateGroup(ConditionGroup group, bool underNegation)
        {
            var negated = underNegation ^ group.Negate;
            var parts = group.Conditions.Select(c => Translate(c, negated)).ToList();
            var joined = string.Join(LogicalOperators.IsOr(group.Logic) ? " OR " : " AND ", parts);
            var sql = $"({joined})";
            return group.Negate ? $"(NOT {sql})" : sql;
        }

        private string TranslateRule(ConditionRule rule, bool underNegation)
        {
            var field = catalog.ResolvePath(root.Key, rule.Field);
            var column = ColumnExpression(field);
            AddColumn(field, isRuleField: true);

            var op = OperatorCatalog.Find(rule.Operator)!;
            var type = field.Field.DataType;
            ResolvedField? compareField = null;

            string predicate;
            if (!string.IsNullOrWhiteSpace(rule.CompareField))
            {
                compareField = catalog.ResolvePath(root.Key, rule.CompareField);
                AddColumn(compareField, isRuleField: true);
                predicate = Comparison(op.Key, column, ColumnExpression(compareField));
            }
            else
            {
                predicate = op.Key switch
                {
                    OperatorCatalog.Between =>
                        $"{column} BETWEEN {Param(rule.Value!.Value, type)} AND {Param(rule.ValueTo!.Value, type)}",

                    OperatorCatalog.In =>
                        $"{column} IN ({string.Join(", ", rule.Values!.Select(v => Param(v, type)))})",

                    OperatorCatalog.NotIn =>
                        $"{column} NOT IN ({string.Join(", ", rule.Values!.Select(v => Param(v, type)))})",

                    OperatorCatalog.Contains => $"{column} LIKE {LikeParam(rule.Value!.Value, "%", "%")}",
                    OperatorCatalog.NotContains => $"{column} NOT LIKE {LikeParam(rule.Value!.Value, "%", "%")}",
                    OperatorCatalog.StartsWith => $"{column} LIKE {LikeParam(rule.Value!.Value, "", "%")}",
                    OperatorCatalog.EndsWith => $"{column} LIKE {LikeParam(rule.Value!.Value, "%", "")}",

                    // For text, "empty" also means a string of spaces. LEN ignores trailing spaces.
                    OperatorCatalog.IsEmpty => type == FieldDataType.String
                        ? $"({column} IS NULL OR LEN({column}) = 0)"
                        : $"{column} IS NULL",
                    OperatorCatalog.IsNotEmpty => type == FieldDataType.String
                        ? $"({column} IS NOT NULL AND LEN({column}) > 0)"
                        : $"{column} IS NOT NULL",

                    OperatorCatalog.IsTrue => $"{column} = 1",
                    OperatorCatalog.IsFalse => $"{column} = 0",
                    OperatorCatalog.IsInPast => $"{column} < CAST(GETDATE() AS date)",
                    OperatorCatalog.IsInFuture => $"{column} > CAST(GETDATE() AS date)",

                    _ => Comparison(op.Key, column, Param(rule.Value!.Value, type))
                };
            }

            var wrapped = $"({predicate})";
            _probes.Add(new RuleProbe(_probes.Count, $"__r{_probes.Count}", rule, field, compareField, underNegation));
            _probeSql.Add(wrapped);
            return wrapped;
        }

        private static string Comparison(string op, string left, string right) => op switch
        {
            OperatorCatalog.EqualsOp => $"{left} = {right}",
            OperatorCatalog.NotEquals => $"{left} <> {right}",
            OperatorCatalog.GreaterThan => $"{left} > {right}",
            OperatorCatalog.GreaterOrEqual => $"{left} >= {right}",
            OperatorCatalog.LessThan => $"{left} < {right}",
            OperatorCatalog.LessOrEqual => $"{left} <= {right}",
            _ => throw new InvalidOperationException($"Operator '{op}' is not a comparison.")
        };

        private string Param(System.Text.Json.JsonElement element, FieldDataType type)
        {
            var name = $"@p{_parameters.Count}";
            _parameters.Add(new QueryParameter(name, ValueConverter.Convert(element, type), type));
            return name;
        }

        /// <summary>LIKE with escaping: % _ [ typed by the user are matched literally, not as wildcards.</summary>
        private string LikeParam(System.Text.Json.JsonElement element, string prefix, string suffix)
        {
            var raw = (string)ValueConverter.Convert(element, FieldDataType.String);
            var escaped = raw.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            var name = $"@p{_parameters.Count}";
            _parameters.Add(new QueryParameter(name, prefix + escaped + suffix, FieldDataType.String));
            return name;
        }

        /// <summary>Returns "alias.[Column]", adding any JOINs the path needs (once per path).</summary>
        private string ColumnExpression(ResolvedField field)
        {
            var alias = EnsureJoins(field.Steps);
            return $"{alias}.{SqlIdentifier.Quote(field.Field.ColumnName)}";
        }

        private string EnsureJoins(IReadOnlyList<RelationStep> steps)
        {
            var path = "";
            var parentAlias = BaseAlias;
            foreach (var step in steps)
            {
                path = path.Length == 0 ? step.Relation.NavigationKey : $"{path}.{step.Relation.NavigationKey}";
                if (!_joinAliases.TryGetValue(path, out var alias))
                {
                    alias = $"j{_joinAliases.Count}";
                    _joinAliases[path] = alias;
                    _joins.Add(
                        $"LEFT JOIN {SqlIdentifier.QuoteTable(step.To.SchemaName, step.To.TableName)} AS {alias} " +
                        $"ON {alias}.{SqlIdentifier.Quote(step.Relation.ToColumn)} = {parentAlias}.{SqlIdentifier.Quote(step.Relation.FromColumn)}");
                }
                parentAlias = alias;
            }
            return parentAlias;
        }

        private void AddColumn(ResolvedField field, bool isRuleField, bool isTitle = false)
        {
            if (!_columnPaths.Add(field.Path))
            {
                // Already selected; just upgrade the flags if needed.
                var index = _columns.FindIndex(c => string.Equals(c.Path, field.Path, StringComparison.OrdinalIgnoreCase));
                var existing = _columns[index];
                _columns[index] = existing with
                {
                    IsRuleField = existing.IsRuleField || isRuleField,
                    IsTitle = existing.IsTitle || isTitle
                };
                return;
            }
            _columns.Add(new ResultColumn($"c{_columns.Count}", field.Path, field, field.GroupLabel(root), isRuleField, isTitle));
        }

        public BuiltQuery Compose(string where, int maxRows)
        {
            // Default display columns of the base entity + title fields. Rule fields were already added during translation.
            foreach (var titleKey in root.TitleFieldKeys)
                if (root.FindField(titleKey) is not null)
                    AddColumn(catalog.ResolvePath(root.Key, titleKey), isRuleField: false, isTitle: true);

            foreach (var field in root.Fields.Where(f => f.ShowInResults).OrderBy(f => f.SortOrder))
                AddColumn(catalog.ResolvePath(root.Key, field.Key), isRuleField: false);

            // Display order: base entity fields by SortOrder, then fields from related entities.
            var ordered = _columns
                .OrderBy(c => c.Field.Steps.Count)
                .ThenBy(c => c.Field.Steps.Count == 0 ? c.Field.Field.SortOrder : 0)
                .ToList();

            var pk = $"{BaseAlias}.{SqlIdentifier.Quote(root.PrimaryKeyColumn)}";
            var sql = new StringBuilder();
            sql.Append("SELECT TOP (@__maxRows)\n");
            sql.Append($"    {pk} AS [{BuiltQuery.IdAlias}],\n");
            sql.Append($"    COUNT(*) OVER () AS [{BuiltQuery.TotalAlias}]");

            foreach (var column in ordered)
                sql.Append($",\n    {ColumnExpression(column.Field)} AS [{column.Alias}]");

            for (var i = 0; i < _probes.Count; i++)
                sql.Append($",\n    CASE WHEN {_probeSql[i]} THEN 1 ELSE 0 END AS [{_probes[i].FlagAlias}]");

            sql.Append($"\nFROM {SqlIdentifier.QuoteTable(root.SchemaName, root.TableName)} AS {BaseAlias}");
            foreach (var join in _joins)
                sql.Append($"\n{join}");
            sql.Append($"\nWHERE {where}");
            sql.Append($"\nORDER BY {pk}");

            var parameters = new List<QueryParameter>(_parameters)
            {
                new("@__maxRows", maxRows, FieldDataType.Number)
            };

            return new BuiltQuery(sql.ToString(), parameters, root, ordered, _probes, maxRows);
        }
    }
}
