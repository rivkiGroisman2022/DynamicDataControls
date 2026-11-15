"""
The contract between the .NET server and the AI service (Pydantic models).

Identical to the C# records in DataControls.Core/Ai/AiModels.cs and to the definition contract
(ControlDefinition). JSON field names are camelCase on both sides.
"""

from __future__ import annotations

from typing import Annotated, Literal, Union

from pydantic import BaseModel, ConfigDict, Field
from pydantic.alias_generators import to_camel

DataType = Literal["string", "number", "date", "boolean"]
Severity = Literal["low", "medium", "high"]
JsonValue = Union[bool, int, float, str, None]


class ApiModel(BaseModel):
    """Base: camelCase in JSON, snake_case in Python."""

    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


# ---------------------------------------------------------------------------
# Data structure context (what the server sends - business terms only, no table names)
# ---------------------------------------------------------------------------


class FieldOption(ApiModel):
    value: str
    label: str


class OperatorInfo(ApiModel):
    key: str
    label: str
    date_label: str | None = None
    data_types: list[DataType]
    arity: Literal["none", "single", "range", "list"]
    supports_field_comparison: bool


class FieldContext(ApiModel):
    path: str
    display_name: str
    entity_key: str
    entity_display_name: str
    group_label: str
    data_type: DataType
    unit: str | None = None
    description: str | None = None
    synonyms: list[str] = []
    entity_synonyms: list[str] = []
    options: list[FieldOption] = []
    operators: list[str] = []

    @property
    def depth(self) -> int:
        return self.path.count(".")


class EntityContext(ApiModel):
    key: str
    display_name: str
    display_name_plural: str
    description: str | None = None
    synonyms: list[str] = []
    fields: list[FieldContext]

    def field(self, path: str) -> FieldContext | None:
        return next((f for f in self.fields if f.path == path), None)


class SchemaContext(ApiModel):
    language: str = "he"
    today: str
    entities: list[EntityContext]
    operators: list[OperatorInfo]

    def entity(self, key: str) -> EntityContext | None:
        return next((e for e in self.entities if e.key.lower() == key.lower()), None)

    def operator(self, key: str) -> OperatorInfo | None:
        return next((o for o in self.operators if o.key == key), None)


# ---------------------------------------------------------------------------
# Control definition (the same contract the UI produces)
# ---------------------------------------------------------------------------


class ConditionRule(ApiModel):
    type: Literal["rule"] = "rule"
    field: str
    operator: str
    value: JsonValue = None
    value_to: JsonValue = None
    values: list[JsonValue] | None = None
    compare_field: str | None = None


class ConditionGroup(ApiModel):
    type: Literal["group"] = "group"
    logic: Literal["and", "or"] = "and"
    negate: bool = False
    conditions: list[ConditionNode]


ConditionNode = Annotated[Union[ConditionGroup, ConditionRule], Field(discriminator="type")]
ConditionGroup.model_rebuild()


class ControlDefinition(ApiModel):
    version: int = 1
    entity: str
    condition: ConditionNode


# ---------------------------------------------------------------------------
# Requests and responses
# ---------------------------------------------------------------------------


class SuggestControlRequest(ApiModel):
    text: str = Field(min_length=1, max_length=1000)
    schema_context: SchemaContext = Field(alias="schema")


class QualityRequest(ApiModel):
    schema_context: SchemaContext = Field(alias="schema")
    entity: str | None = None


class ControlSuggestion(ApiModel):
    """The structure of the answer expected from the AI - the response contract (any provider returns exactly this)."""

    name: str
    description: str | None = None
    severity: Severity = "medium"
    definition: ControlDefinition
    explanation: list[str] = []
    confidence: float = Field(ge=0, le=1)
    warnings: list[str] = []
    provider: str
    model: str | None = None


class QualityResponse(ApiModel):
    suggestions: list[ControlSuggestion]
