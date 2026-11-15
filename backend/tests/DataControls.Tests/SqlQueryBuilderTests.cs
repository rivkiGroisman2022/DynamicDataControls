using DataControls.Core.Exceptions;
using DataControls.Core.Query;
using static DataControls.Tests.TestData;

namespace DataControls.Tests;

public class SqlQueryBuilderTests
{
    private readonly SqlQueryBuilder _builder = new();

    [Fact]
    public void Simple_and_condition_produces_parameterized_where()
    {
        var q = _builder.Build(Def("Employee", And(Rule("age", "lessThan", 18), Rule("salary", "greaterThan", 15000))), Catalog());

        Assert.Contains("FROM [dbo].[Employees] AS t0", q.Sql);
        Assert.Contains("WHERE ((t0.[Age] < @p0) AND (t0.[Salary] > @p1))", q.Sql);
        Assert.Equal(18m, q.Parameters.Single(p => p.Name == "@p0").Value);
        Assert.Equal(15000m, q.Parameters.Single(p => p.Name == "@p1").Value);
        Assert.DoesNotContain("JOIN", q.Sql);
    }

    [Fact]
    public void Or_group_nested_inside_and_keeps_precedence_with_parentheses()
    {
        var q = _builder.Build(Def("Employee",
            And(Rule("isActive", "isTrue"), Or(Rule("email", "isEmpty"), Rule("salary", "isEmpty")))), Catalog());

        Assert.Contains(
            "WHERE ((t0.[IsActive] = 1) AND (((t0.[Email] IS NULL OR LEN(t0.[Email]) = 0)) OR (t0.[Salary] IS NULL)))",
            q.Sql);
    }

    [Fact]
    public void Field_to_field_comparison_uses_no_parameter()
    {
        var q = _builder.Build(Def("Employee", Rule("endDate", "lessThan", compareField: "startDate")), Catalog());

        Assert.Contains("WHERE (t0.[EmploymentEndDate] < t0.[EmploymentStartDate])", q.Sql);
        Assert.DoesNotContain(q.Parameters, p => p.Name.StartsWith("@p"));
    }

    [Fact]
    public void Relation_path_adds_left_join_once_even_when_used_twice()
    {
        var q = _builder.Build(Def("Employee",
            And(Rule("department.isActive", "isFalse"), Rule("department.name", "startsWith", "רכש"))), Catalog());

        Assert.Single(q.Sql.Split('\n'), l => l.StartsWith("LEFT JOIN [dbo].[Departments]"));
        Assert.Contains("LEFT JOIN [dbo].[Departments] AS j1 ON j1.[DepartmentId] = t0.[DepartmentId]", q.Sql);
    }

    [Fact]
    public void Two_level_relation_chains_joins()
    {
        var q = _builder.Build(Def("Employee", Rule("department.branch.isActive", "isFalse")), Catalog());

        Assert.Contains("LEFT JOIN [dbo].[Departments] AS j1 ON j1.[DepartmentId] = t0.[DepartmentId]", q.Sql);
        Assert.Contains("LEFT JOIN [dbo].[Branches] AS j2 ON j2.[BranchId] = j1.[BranchId]", q.Sql);
        Assert.Contains("WHERE (j2.[IsActive] = 0)", q.Sql);
    }

    [Fact]
    public void Malicious_value_stays_in_parameter_and_never_reaches_sql_text()
    {
        const string attack = "x'; DROP TABLE dbo.Employees; --";
        var q = _builder.Build(Def("Employee", Rule("lastName", "equals", attack)), Catalog());

        Assert.DoesNotContain("DROP", q.Sql);
        Assert.Equal(attack, q.Parameters.Single(p => p.Name == "@p0").Value);
        Assert.Null(SqlSafetyGuard.FindProblem(q.Sql));
    }

    [Fact]
    public void Like_wildcards_typed_by_user_are_escaped()
    {
        var q = _builder.Build(Def("Employee", Rule("email", "contains", "50%_off")), Catalog());

        Assert.Equal("%50[%][_]off%", q.Parameters.Single(p => p.Name == "@p0").Value);
    }

    [Fact]
    public void In_operator_creates_parameter_per_value()
    {
        var q = _builder.Build(Def("Employee", Rule("age", "in", values: [16, 17])), Catalog());

        Assert.Contains("t0.[Age] IN (@p0, @p1)", q.Sql);
    }

    [Fact]
    public void Each_rule_gets_a_probe_column_for_reasons()
    {
        var q = _builder.Build(Def("Employee", Or(Rule("age", "lessThan", 18), Rule("salary", "lessThan", 0))), Catalog());

        Assert.Equal(2, q.Rules.Count);
        Assert.Contains("CASE WHEN (t0.[Age] < @p0) THEN 1 ELSE 0 END AS [__r0]", q.Sql);
        Assert.Contains("CASE WHEN (t0.[Salary] < @p1) THEN 1 ELSE 0 END AS [__r1]", q.Sql);
    }

    [Fact]
    public void Result_includes_total_count_and_row_limit()
    {
        var q = _builder.Build(Def("Employee", Rule("age", "lessThan", 18)), Catalog(), maxRows: 50);

        Assert.StartsWith("SELECT TOP (@__maxRows)", q.Sql);
        Assert.Contains("COUNT(*) OVER () AS [__total]", q.Sql);
        Assert.Equal(50, q.Parameters.Single(p => p.Name == "@__maxRows").Value);
    }

    [Fact]
    public void Hidden_rule_fields_are_still_returned_so_the_user_sees_the_value()
    {
        // email has ShowInResults=false, but because a rule uses it, it must appear in the results
        var q = _builder.Build(Def("Employee", Rule("email", "isEmpty")), Catalog());

        Assert.Contains(q.Columns, c => c.Path == "email" && c.IsRuleField);
    }

    [Fact]
    public void Negated_group_wraps_with_not()
    {
        var group = And(Rule("age", "greaterOrEqual", 18));
        var q = _builder.Build(Def("Employee", group with { Negate = true }), Catalog());

        Assert.Contains("WHERE (NOT ((t0.[Age] >= @p0)))", q.Sql);
        Assert.True(q.Rules[0].IsUnderNegation);
    }

    [Fact]
    public void Invalid_definition_is_rejected_before_any_sql_is_built()
    {
        Assert.Throws<DefinitionValidationException>(() =>
            _builder.Build(Def("Employee", Rule("doesNotExist", "equals", 1)), Catalog()));
    }
}
