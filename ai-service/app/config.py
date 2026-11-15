"""Settings from environment variables. Replacing the model = changing AI_PROVIDER, without changing code."""

from __future__ import annotations

import os
from dataclasses import dataclass


@dataclass(frozen=True)
class Settings:
    #: "mock" (default, no external calls) or "claude".
    provider: str = os.getenv("AI_PROVIDER", "mock").lower()
    claude_model: str = os.getenv("CLAUDE_MODEL", "claude-opus-5-5")
    claude_effort: str = os.getenv("CLAUDE_EFFORT", "medium")


settings = Settings()
