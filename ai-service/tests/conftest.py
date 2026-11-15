import json
from pathlib import Path

import pytest

from app.schemas import SchemaContext

FIXTURE = Path(__file__).parent / "fixtures" / "schema_context.json"


@pytest.fixture(scope="session")
def schema_json() -> dict:
    """The real context, exported from the .NET server (GET /api/ai/schema-context) - proves both sides share the same contract."""
    return json.loads(FIXTURE.read_text(encoding="utf-8"))


@pytest.fixture(scope="session")
def schema(schema_json) -> SchemaContext:
    return SchemaContext.model_validate(schema_json)
