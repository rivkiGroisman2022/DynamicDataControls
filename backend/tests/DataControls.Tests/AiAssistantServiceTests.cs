using DataControls.Core.Abstractions;
using DataControls.Core.Ai;
using DataControls.Core.Definitions;
using DataControls.Core.Exceptions;
using DataControls.Core.Metadata;
using static DataControls.Tests.TestData;

namespace DataControls.Tests;

/// <summary>The AI only suggests: every suggestion goes through the same validator as a manual control, and nothing runs automatically.</summary>
public class AiAssistantServiceTests
{
    private sealed class FakeMetadata : IMetadataProvider
    {
        public Task<MetadataCatalog> GetCatalogAsync(CancellationToken ct = default) => Task.FromResult(Catalog());
        public void Invalidate() { }
    }

    private sealed class FakeAi(ControlDefinition definition, string? severity = "high") : IAiServiceClient
    {
        public AiSuggestControlRequest? LastRequest { get; private set; }

        public Task<AiControlSuggestion> SuggestControlAsync(AiSuggestControlRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            return Task.FromResult(new AiControlSuggestion("קטינים", "תיאור", severity, definition, ["הסבר"], 0.9, [], "fake", null));
        }

        public Task<IReadOnlyList<AiControlSuggestion>> SuggestQualityControlsAsync(AiQualityRequest request, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiControlSuggestion>>(
            [
                new("טוב", null, "low", Def("Employee", Rule("age", "lessThan", 18)), null, 0.8, null, "fake", null),
                new("לא קיים", null, "low", Def("Employee", Rule("shoeSize", "equals", 1)), null, 0.8, null, "fake", null),
            ]);
    }

    private static AiAssistantService Service(IAiServiceClient ai) => new(new FakeMetadata(), ai, TimeProvider.System);

    [Fact]
    public async Task Valid_suggestion_gets_description_for_user_approval()
    {
        var ai = new FakeAi(Def("Employee", And(Rule("age", "lessThan", 18), Rule("salary", "greaterThan", 15000))));

        var result = await Service(ai).SuggestControlAsync("מצא קטינים עם שכר גבוה");

        Assert.True(result.IsValid);
        Assert.Equal("עובדים – גיל קטן מ־18 וגם שכר חודשי גדול מ־15,000 ש\"ח", result.DefinitionText);
        Assert.Equal("fake", result.Provider);
    }

    [Fact]
    public async Task Suggestion_referring_to_unknown_field_is_marked_invalid_not_trusted()
    {
        var ai = new FakeAi(Def("Employee", Rule("password", "isEmpty")));

        var result = await Service(ai).SuggestControlAsync("משהו");

        Assert.False(result.IsValid);
        Assert.Null(result.DefinitionText);
        Assert.Contains(result.Errors, e => e.Path.EndsWith(".field"));
    }

    [Fact]
    public async Task Context_sent_to_ai_contains_business_terms_and_relation_paths_but_no_table_names()
    {
        var ai = new FakeAi(Def("Employee", Rule("age", "lessThan", 18)));

        await Service(ai).SuggestControlAsync("x");

        var employee = ai.LastRequest!.Schema.Entities.Single(e => e.Key == "Employee");
        Assert.Contains(employee.Fields, f => f.Path == "department.branch.isActive" && f.GroupLabel == "עובד › מחלקה › סניף");
        var json = System.Text.Json.JsonSerializer.Serialize(ai.LastRequest.Schema);
        Assert.DoesNotContain("Employees", json);          // physical table name
        Assert.DoesNotContain("EmploymentStartDate", json); // physical column name
    }

    [Fact]
    public async Task Unknown_severity_falls_back_to_medium()
    {
        var ai = new FakeAi(Def("Employee", Rule("age", "lessThan", 18)), severity: "catastrophic");
        Assert.Equal("medium", (await Service(ai).SuggestControlAsync("x")).Severity);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Empty_text_is_rejected_before_calling_ai(string text)
    {
        var ai = new FakeAi(Def("Employee", Rule("age", "lessThan", 18)));

        await Assert.ThrowsAsync<DefinitionValidationException>(() => Service(ai).SuggestControlAsync(text));
        Assert.Null(ai.LastRequest);
    }

    [Fact]
    public async Task Quality_suggestions_keep_only_valid_ones()
    {
        var ai = new FakeAi(Def("Employee", Rule("age", "lessThan", 18)));

        var result = await Service(ai).SuggestQualityControlsAsync(null);

        Assert.Equal(["טוב"], result.Select(r => r.Name));
    }
}
