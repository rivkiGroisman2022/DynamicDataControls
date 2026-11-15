using DataControls.Core.Definitions;
using DataControls.Core.Exceptions;
using DataControls.Core.Metadata;
using DataControls.Core.Query;

namespace DataControls.Tests;

public class SafetyAndJsonTests
{
    [Theory]
    [InlineData("SELECT 1; DROP TABLE x")]
    [InlineData("DELETE FROM dbo.Employees")]
    [InlineData("SELECT * FROM t -- comment")]
    [InlineData("SELECT * INTO dbo.Copy FROM dbo.Employees")]
    [InlineData("SELECT * FROM t WHERE a = 'literal'")]
    [InlineData("SELECT 1 EXEC xp_cmdshell")]
    public void Safety_guard_rejects_dangerous_sql(string sql) =>
        Assert.NotNull(SqlSafetyGuard.FindProblem(sql));

    [Fact]
    public void Safety_guard_ignores_keywords_inside_bracketed_identifiers() =>
        Assert.Null(SqlSafetyGuard.FindProblem("SELECT t0.[UpdatedAt], t0.[DeleteFlag] FROM [dbo].[T] AS t0"));

    [Theory]
    [InlineData("Salary")]
    [InlineData("Employment_Start_Date")]
    public void Valid_identifier_is_bracketed(string name) =>
        Assert.Equal($"[{name}]", SqlIdentifier.Quote(name));

    [Theory]
    [InlineData("Salary]; DROP TABLE x; --")]
    [InlineData("1abc")]
    [InlineData("a b")]
    [InlineData("")]
    public void Invalid_identifier_in_metadata_is_rejected(string name) =>
        Assert.Throws<MetadataConfigurationException>(() => SqlIdentifier.Quote(name));

    [Fact]
    public void Definition_json_round_trips()
    {
        const string json = """
            {"version":1,"entity":"Employee","condition":{"type":"group","logic":"or","conditions":[
              {"type":"rule","field":"age","operator":"lessThan","value":18},
              {"type":"rule","field":"endDate","operator":"lessThan","compareField":"startDate"}]}}
            """;

        var definition = DefinitionJson.Deserialize(json);
        var again = DefinitionJson.Deserialize(DefinitionJson.Serialize(definition));

        var group = Assert.IsType<ConditionGroup>(again.Condition);
        Assert.Equal("or", group.Logic);
        Assert.Equal("startDate", Assert.IsType<ConditionRule>(group.Conditions[1]).CompareField);
    }

    [Fact]
    public void Type_discriminator_may_appear_anywhere_in_object()
    {
        // AI output does not guarantee property order
        var definition = DefinitionJson.Deserialize(
            """{"entity":"Employee","condition":{"field":"age","operator":"isEmpty","type":"rule"}}""");

        Assert.IsType<ConditionRule>(definition.Condition);
    }

    [Fact]
    public void Broken_json_gives_friendly_error() =>
        Assert.Throws<InvalidDefinitionFormatException>(() => DefinitionJson.Deserialize("{ not json"));

    [Fact]
    public void Reachable_fields_include_related_entities_without_cycles()
    {
        var fields = TestData.Catalog().GetReachableFields("Employee");

        Assert.Contains(fields, f => f.Path == "salary");
        Assert.Contains(fields, f => f.Path == "department.isActive");
        Assert.Contains(fields, f => f.Path == "department.branch.name");
        Assert.Equal("עובד › מחלקה › סניף",
            fields.Single(f => f.Path == "department.branch.name").GroupLabel(TestData.Catalog().GetEntity("Employee")));
    }

    [Fact]
    public void Path_resolution_is_case_insensitive_and_canonical()
    {
        Assert.True(TestData.Catalog().TryResolvePath("employee", "Department.IsActive", out var field, out _));
        Assert.Equal("department.isActive", field!.Path);
        Assert.Equal(FieldDataType.Boolean, field.Field.DataType);
    }
}
