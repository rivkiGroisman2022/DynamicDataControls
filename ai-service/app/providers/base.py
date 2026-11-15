"""
The AI provider interface. Every model (Mock, Claude, any other) implements exactly this interface
and returns exactly the same ControlSuggestion contract - so the model can be replaced through
configuration alone (AI_PROVIDER), without touching the control logic in .NET.
"""

from __future__ import annotations

from abc import ABC, abstractmethod

from app.schemas import ControlSuggestion, SchemaContext


class AiUnderstandingError(Exception):
    """The model did not manage to understand the request (e.g. no conditions identified). Returned to the user as 422."""


class AiProviderError(Exception):
    """The provider itself failed (network, quota, invalid answer). Returned as 502."""


class AiProvider(ABC):
    #: Provider name - recorded in every suggestion, so it is known who generated it.
    name: str = "base"
    model: str | None = None

    @abstractmethod
    def suggest_control(self, text: str, schema: SchemaContext) -> ControlSuggestion:
        """Free text describing a business requirement → a control suggestion."""

    @abstractmethod
    def suggest_quality_controls(self, schema: SchemaContext, entity: str | None = None) -> list[ControlSuggestion]:
        """Analyzes the data structure and proactively suggests data-quality controls (advanced task)."""
