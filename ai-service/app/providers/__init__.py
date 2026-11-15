"""Chooses the provider from configuration (Factory). This is the only place that knows which model is in use."""

from __future__ import annotations

from functools import lru_cache

from app.config import settings
from app.providers.base import AiProvider


@lru_cache(maxsize=1)
def get_provider() -> AiProvider:
    if settings.provider == "mock":
        from app.providers.mock_provider import MockProvider

        return MockProvider()
    if settings.provider == "claude":
        from app.providers.claude_provider import ClaudeProvider

        return ClaudeProvider(model=settings.claude_model, effort=settings.claude_effort)
    raise ValueError(f"Unknown AI_PROVIDER '{settings.provider}'. Use 'mock' or 'claude'.")
