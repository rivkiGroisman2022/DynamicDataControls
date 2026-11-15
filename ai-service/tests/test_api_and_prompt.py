import pytest
from fastapi.testclient import TestClient

from app.main import app
from app.prompt_builder import build_system_prompt
from app.providers import get_provider
from app.providers.base import AiProvider, AiProviderError
from app.providers.claude_provider import ClaudeProvider, LlmControl, LlmGroup, LlmRule
from app.quality_advisor import suggest_quality_controls


@pytest.fixture
def client() -> TestClient:
    return TestClient(app)


def test_health_reports_provider(client):
    assert client.get("/health").json()["provider"] == "mock"


def test_suggest_control_returns_camel_case_contract(client, schema_json):
    response = client.post("/v1/suggest-control", json={"text": "עובדים שתאריך סיום העסקתם מוקדם מתאריך תחילת העסקתם", "schema": schema_json})

    assert response.status_code == 200
    body = response.json()
    rule = body["definition"]["condition"]["conditions"][0]
    assert rule == {"type": "rule", "field": "endDate", "operator": "lessThan", "compareField": "startDate"}
    assert {"name", "explanation", "confidence", "warnings", "provider"} <= body.keys()


def test_not_understood_returns_422(client, schema_json):
    response = client.post("/v1/suggest-control", json={"text": "שלום", "schema": schema_json})
    assert response.status_code == 422


def test_empty_text_is_rejected_by_contract(client, schema_json):
    assert client.post("/v1/suggest-control", json={"text": "", "schema": schema_json}).status_code == 422


def test_provider_failure_returns_502(client, schema_json):
    class Broken(AiProvider):
        name = "broken"

        def suggest_control(self, text, schema):
            raise AiProviderError("down")

        def suggest_quality_controls(self, schema, entity=None):
            raise AiProviderError("down")

    app.dependency_overrides[get_provider] = lambda: Broken()
    try:
        response = client.post("/v1/suggest-control", json={"text": "עובדים", "schema": schema_json})
        assert response.status_code == 502
    finally:
        app.dependency_overrides.clear()


def test_quality_suggestions_for_one_entity(client, schema_json):
    response = client.post("/v1/suggest-quality-controls", json={"schema": schema_json, "entity": "Project"})
    names = [s["name"] for s in response.json()["suggestions"]]
    assert any("תאריך סיום" in n for n in names) and any("שלילי" in n for n in names)


def test_quality_advisor_only_uses_existing_paths(schema):
    for suggestion in suggest_quality_controls(schema, None, "mock", None):
        entity = schema.entity(suggestion.definition.entity)
        stack = [suggestion.definition.condition]
        while stack:
            node = stack.pop()
            if node.type == "group":
                stack.extend(node.conditions)
            else:
                assert entity.field(node.field), node.field
                assert node.compare_field is None or entity.field(node.compare_field)


def test_prompt_contains_business_catalog_but_no_table_names(schema):
    prompt = build_system_prompt(schema)
    assert "department.isActive" in prompt and "שכר חודשי" in prompt
    assert "Employees" not in prompt and "DepartmentId" not in prompt    # no physical names
    assert schema.today in prompt


def test_prompt_preview_exposes_response_schema(client, schema_json):
    body = client.post("/v1/prompt-preview", json={"text": "עובדים", "schema": schema_json}).json()
    assert body["responseSchema"]["additionalProperties"] is False
    assert "<requirement>" in body["user"]


def test_claude_output_is_converted_to_the_same_contract():
    """The conversion from the model's output to the contract - testable without an API call."""
    out = LlmControl(
        name="x", description="d", severity="high", entity="Employee", logic="and", negate=False,
        conditions=[
            LlmRule(type="rule", field="isActive", operator="isTrue", value=None, valueTo=None, values=None, compareField=None),
            LlmGroup(type="group", logic="or", negate=False, conditions=[
                LlmRule(type="rule", field="email", operator="isEmpty", value=None, valueTo=None, values=None, compareField=None),
            ]),
        ],
        explanation=["e"], confidence=1.4, warnings=[],
    )
    suggestion = ClaudeProvider()._to_suggestion(out)

    assert suggestion.provider == "claude"
    assert suggestion.confidence == 1.0                  # clamped to the valid range
    assert suggestion.definition.condition.conditions[1].logic == "or"


def test_claude_schema_is_strict_for_structured_outputs():
    def walk(node):
        if isinstance(node, dict):
            if node.get("type") == "object":
                assert node.get("additionalProperties") is False
                assert set(node.get("required", [])) == set(node.get("properties", {}))
            for value in node.values():
                walk(value)
        elif isinstance(node, list):
            for value in node:
                walk(value)

    walk(LlmControl.model_json_schema())
