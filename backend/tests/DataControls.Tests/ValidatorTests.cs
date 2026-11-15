using DataControls.Core.Definitions;
using DataControls.Core.Validation;
using static DataControls.Tests.TestData;

namespace DataControls.Tests;

public class ValidatorTests
{
    private readonly ControlDefinitionValidator _validator = new();

    [Fact]
    public void Valid_definition_passes()
    {
        var result = _validator.Validate(
            Def("Employee", And(Rule("age", "lessThan", 18), Rule("department.isActive", "isFalse"))), Catalog());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Unknown_entity_is_reported()
    {
        var result = _validator.Validate(Def("Spaceship", Rule("age", "lessThan", 18)), Catalog());

        Assert.Contains(result.Errors, e => e.Path == "entity");
    }

    [Fact]
    public void Unknown_field_and_unknown_relation_are_reported_with_path()
    {
        var result = _validator.Validate(
            Def("Employee", And(Rule("shoeSize", "equals", 40), Rule("manager.name", "isEmpty"))), Catalog());

        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Path == "condition.conditions[0].field");
        Assert.Contains(result.Errors, e => e.Path == "condition.conditions[1].field");
    }

    [Fact]
    public void Operator_must_fit_field_type()
    {
        var result = _validator.Validate(Def("Employee", Rule("salary", "contains", "abc")), Catalog());

        Assert.Contains(result.Errors, e => e.Path.EndsWith(".operator"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_text_value_is_rejected_because_contains_would_match_everything(string value)
    {
        var result = _validator.Validate(Def("Employee", Rule("email", "contains", value)), Catalog());

        Assert.Contains(result.Errors, e => e.Path == "condition.value");
    }

    [Fact]
    public void Value_outside_closed_option_list_is_rejected()
    {
        var invalid = _validator.Validate(Def("Branch", Rule("region", "equals", "Moon")), Catalog());
        var invalidInList = _validator.Validate(Def("Branch", Rule("region", "in", values: ["North", "Moon"])), Catalog());
        var valid = _validator.Validate(Def("Branch", Rule("region", "equals", "North")), Catalog());
        var freeText = _validator.Validate(Def("Branch", Rule("region", "contains", "or")), Catalog());

        Assert.Contains(invalid.Errors, e => e.Path == "condition.value" && e.Message.Contains("צפון"));
        Assert.Contains(invalidInList.Errors, e => e.Path == "condition.values[1]");
        Assert.True(valid.IsValid);
        Assert.True(freeText.IsValid);
    }

    [Fact]
    public void Huge_number_is_reported_as_too_large_not_as_not_a_number()
    {
        var result = _validator.Validate(Def("Employee", Rule("salary", "greaterThan", 1e40)), Catalog());

        Assert.Contains(result.Errors, e => e.Message.Contains("גדול מדי"));
    }

    [Fact]
    public void Value_must_match_field_type()
    {
        var result = _validator.Validate(Def("Employee", Rule("age", "lessThan", "eighteen")), Catalog());

        Assert.Contains(result.Errors, e => e.Path.EndsWith(".value"));
    }

    [Fact]
    public void Missing_value_is_reported()
    {
        var result = _validator.Validate(Def("Employee", Rule("age", "lessThan")), Catalog());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Comparing_fields_of_different_types_is_rejected()
    {
        var result = _validator.Validate(Def("Employee", Rule("salary", "lessThan", compareField: "startDate")), Catalog());

        Assert.Contains(result.Errors, e => e.Path.EndsWith(".compareField"));
    }

    [Fact]
    public void Between_with_reversed_range_is_rejected()
    {
        var result = _validator.Validate(Def("Employee", Rule("age", "between", 60, 20)), Catalog());

        Assert.Contains(result.Errors, e => e.Path.EndsWith(".valueTo"));
    }

    [Fact]
    public void Empty_group_is_rejected()
    {
        var result = _validator.Validate(Def("Employee", new ConditionGroup { Logic = "and" }), Catalog());

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Too_deep_nesting_is_rejected()
    {
        ConditionNode node = Rule("age", "lessThan", 18);
        for (var i = 0; i < ControlDefinitionValidator.MaxDepth + 1; i++) node = And(node);

        var result = _validator.Validate(Def("Employee", node), Catalog());

        Assert.Contains(result.Errors, e => e.Message.Contains("עמוק"));
    }

    [Fact]
    public void All_errors_are_collected_not_just_the_first()
    {
        var result = _validator.Validate(Def("Employee",
            And(Rule("x", "equals", 1), Rule("age", "nope", 1), Rule("salary", "lessThan", "abc"))), Catalog());

        Assert.Equal(3, result.Errors.Count);
    }
}
