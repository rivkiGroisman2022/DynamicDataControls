"""
A provider for a real language model (Claude, Anthropic SDK) - infrastructure for the future, not enabled by default.

Enabling it: AI_PROVIDER=claude + credentials (ANTHROPIC_API_KEY or an `ant auth login` profile).
No change to .NET is needed - the contract stays the same.

Implementation principles:
- Structured Outputs (output_config.format = json_schema): the API forces the answer to match the schema,
  so there is no need to "pull" JSON out of free text.
- The answer is validated again with Pydantic here, and again with the real validator in .NET - never trusted blindly.
- The system prompt (data structure) is fixed and marked for caching (cache_control) - repeat requests cost less.
- server-side fallbacks: if the model declines a request for safety reasons, the API automatically falls back to another model.
"""

from __future__ import annotations

from typing import Any, Literal, TypeVar

from pydantic import BaseModel, ConfigDict, ValidationError

from app.prompt_builder import build_quality_message, build_system_prompt, build_user_message
from app.providers.base import AiProvider, AiProviderError, AiUnderstandingError
from app.schemas import (
    ConditionGroup,
    ConditionNode,
    ConditionRule,
    ControlDefinition,
    ControlSuggestion,
    SchemaContext,
)

# ---------------------------------------------------------------------------
# The output schema the model must return (Structured Outputs).
# Two levels (a root group + sub-groups) and no recursion - a simple schema the model fills reliably,
# and it covers the common cases ("A and (B or C)").
# ---------------------------------------------------------------------------


class _Strict(BaseModel):
    model_config = ConfigDict(extra="forbid")


class LlmRule(_Strict):
    type: Literal["rule"]
    field: str
    operator: str
    value: str | float | bool | None
    valueTo: str | float | None
    values: list[str | float] | None
    compareField: str | None


class LlmGroup(_Strict):
    type: Literal["group"]
    logic: Literal["and", "or"]
    negate: bool
    conditions: list[LlmRule]


class LlmControl(_Strict):
    name: str
    description: str
    severity: Literal["low", "medium", "high"]
    entity: str
    logic: Literal["and", "or"]
    negate: bool
    conditions: list[LlmRule | LlmGroup]
    explanation: list[str]
    confidence: float
    warnings: list[str]


class LlmQualityOutput(_Strict):
    suggestions: list[LlmControl]


T = TypeVar("T", bound=BaseModel)


class ClaudeProvider(AiProvider):
    name = "claude"

    def __init__(self, model: str = "claude-opus-5-5", effort: str = "medium") -> None:
        self.model = model
        self.effort = effort
        self._client: Any = None

    # ------------------------------------------------------------------ public

    def suggest_control(self, text: str, schema: SchemaContext) -> ControlSuggestion:
        output = self._call(schema, build_user_message(text), LlmControl)
        return self._to_suggestion(output)

    def suggest_quality_controls(self, schema: SchemaContext, entity: str | None = None) -> list[ControlSuggestion]:
        output = self._call(schema, build_quality_message(entity), LlmQualityOutput)
        return [self._to_suggestion(s) for s in output.suggestions]

    # ------------------------------------------------------------------ API call

    def _client_or_create(self) -> Any:
        if self._client is None:
            import anthropic  # imported only when the provider is actually used

            self._client = anthropic.Anthropic()  # credentials from the environment
        return self._client

    def _call(self, schema: SchemaContext, user_message: str, output_model: type[T]) -> T:
        import anthropic

        client = self._client_or_create()
        try:
            response = client.beta.messages.create(
                model=self.model,
                max_tokens=16000,
                betas=["server-side-fallback-2026-07-01"],
                fallbacks="default",
                system=[{
                    "type": "text",
                    "text": build_system_prompt(schema),
                    "cache_control": {"type": "ephemeral"},
                }],
                messages=[{"role": "user", "content": user_message}],
                output_config={
                    "effort": self.effort,
                    "format": {"type": "json_schema", "schema": output_model.model_json_schema()},
                },
            )
        except anthropic.AuthenticationError as e:
            raise AiProviderError("אין הרשאה לשירות ה-AI (מפתח API חסר או לא תקין).") from e
        except anthropic.RateLimitError as e:
            raise AiProviderError("שירות ה-AI עמוס כרגע. נסו שוב בעוד רגע.") from e
        except anthropic.APIStatusError as e:
            raise AiProviderError(f"שירות ה-AI החזיר שגיאה ({e.status_code}).") from e
        except anthropic.APIConnectionError as e:
            raise AiProviderError("אין חיבור לשירות ה-AI.") from e

        if response.stop_reason == "refusal":
            raise AiUnderstandingError("המודל לא יכול לטפל בבקשה הזו. נסו לנסח אותה אחרת.")
        if response.stop_reason == "max_tokens":
            raise AiProviderError("תשובת המודל נקטעה.")

        text = next((block.text for block in response.content if block.type == "text"), None)
        if not text:
            raise AiProviderError("המודל לא החזיר תשובה.")
        try:
            return output_model.model_validate_json(text)
        except ValidationError as e:
            raise AiProviderError("תשובת המודל אינה במבנה הצפוי.") from e

    # ------------------------------------------------------------------ conversion

    def _to_suggestion(self, out: LlmControl) -> ControlSuggestion:
        def rule(r: LlmRule) -> ConditionRule:
            return ConditionRule(field=r.field, operator=r.operator, value=r.value, value_to=r.valueTo,
                                 values=list(r.values) if r.values else None, compare_field=r.compareField)

        conditions: list[ConditionNode] = [
            rule(c) if isinstance(c, LlmRule)
            else ConditionGroup(logic=c.logic, negate=c.negate, conditions=[rule(r) for r in c.conditions])
            for c in out.conditions
        ]
        return ControlSuggestion(
            name=out.name,
            description=out.description,
            severity=out.severity,
            definition=ControlDefinition(entity=out.entity,
                                         condition=ConditionGroup(logic=out.logic, negate=out.negate, conditions=conditions)),
            explanation=out.explanation,
            confidence=min(max(out.confidence, 0.0), 1.0),
            warnings=out.warnings,
            provider=self.name,
            model=self.model,
        )
