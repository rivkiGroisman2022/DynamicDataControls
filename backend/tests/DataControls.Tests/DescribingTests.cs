using DataControls.Core.Describing;
using DataControls.Core.Query;
using static DataControls.Tests.TestData;

namespace DataControls.Tests;

public class DescribingTests
{
    [Fact]
    public void Definition_is_described_in_business_hebrew()
    {
        var text = DefinitionDescriber.Describe(
            Def("Employee", And(Rule("age", "lessThan", 18), Rule("salary", "greaterThan", 15000))), Catalog());

        Assert.Equal("עובדים – גיל קטן מ־18 וגם שכר חודשי גדול מ־15,000 ש\"ח", text);
    }

    [Fact]
    public void Date_comparison_uses_date_wording()
    {
        var text = DefinitionDescriber.Describe(
            Def("Employee", Rule("endDate", "lessThan", compareField: "startDate")), Catalog());

        Assert.Equal("עובדים – תאריך סיום העסקה מוקדם מתאריך תחילת העסקה", text);
    }

    [Fact]
    public void Nested_or_is_wrapped_in_parentheses()
    {
        var text = DefinitionDescriber.Describe(
            Def("Employee", And(Rule("isActive", "isTrue"), Or(Rule("email", "isEmpty"), Rule("salary", "isEmpty")))), Catalog());

        Assert.Equal("עובדים – עובד פעיל: כן וגם (ללא כתובת אימייל או ללא שכר חודשי)", text);
    }

    [Theory]
    [InlineData("קטן מ", "18", "קטן מ־18")]
    [InlineData("מוקדם מ", "תאריך התחלה", "מוקדם מתאריך התחלה")]
    [InlineData("מכיל", "abc", "מכיל abc")]
    public void Hebrew_preposition_attaches_correctly(string label, string text, string expected) =>
        Assert.Equal(expected, HebrewText.Attach(label, text));

    [Fact]
    public void Reasons_list_only_the_rules_that_matched_the_row()
    {
        var query = new SqlQueryBuilder().Build(
            Def("Employee", Or(Rule("age", "lessThan", 18), Rule("salary", "lessThan", 0))), Catalog());

        var ageColumn = query.Columns.Single(c => c.Path == "age").Alias;
        var salaryColumn = query.Columns.Single(c => c.Path == "salary").Alias;
        var row = new Dictionary<string, object?>
        {
            [ageColumn] = 16, [salaryColumn] = 9000m, ["__r0"] = 1, ["__r1"] = 0
        };

        var reasons = ViolationReasonBuilder.Build(query, row);

        Assert.Equal(["גיל: 16 (קטן מ־18)"], reasons);
    }

    [Fact]
    public void Reason_for_field_comparison_shows_both_values()
    {
        var query = new SqlQueryBuilder().Build(
            Def("Employee", Rule("endDate", "lessThan", compareField: "startDate")), Catalog());

        var row = new Dictionary<string, object?>
        {
            [query.Columns.Single(c => c.Path == "endDate").Alias] = new DateTime(2016, 2, 28),
            [query.Columns.Single(c => c.Path == "startDate").Alias] = new DateTime(2018, 5, 1),
            ["__r0"] = 1
        };

        var reason = Assert.Single(ViolationReasonBuilder.Build(query, row));
        Assert.Equal("תאריך סיום העסקה: 28/02/2016 (מוקדם מתאריך תחילת העסקה: 01/05/2018)", reason);
    }

    [Fact]
    public void Negative_numbers_have_no_hidden_direction_marks()
    {
        var field = Catalog().GetEntity("Employee").FindField("salary")!;
        Assert.Equal("-1,200 ש\"ח", HebrewText.FormatValue(-1200m, field));
    }
}
