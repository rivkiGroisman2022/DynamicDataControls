"""
The AI service (FastAPI). Runs separately from the .NET server - a real separation between the AI mechanism and the control mechanism.

Run:  .venv\\Scripts\\python -m uvicorn app.main:app --port 8000
Docs: http://localhost:8000/docs
"""

from __future__ import annotations

import logging

from fastapi import Depends, FastAPI, HTTPException

from app.prompt_builder import build_system_prompt, build_user_message
from app.providers import get_provider
from app.providers.base import AiProvider, AiProviderError, AiUnderstandingError
from app.schemas import ControlSuggestion, QualityRequest, QualityResponse, SuggestControlRequest

logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
log = logging.getLogger("ai-service")

app = FastAPI(
    title="Data Controls AI Service",
    version="1.0.0",
    description="Turns a free-text control description into a control definition (Mock by default).",
)


@app.get("/health")
def health(provider: AiProvider = Depends(get_provider)) -> dict[str, str | None]:
    return {"status": "ok", "provider": provider.name, "model": provider.model}


@app.post("/v1/suggest-control", response_model=ControlSuggestion, response_model_exclude_none=True)
def suggest_control(request: SuggestControlRequest, provider: AiProvider = Depends(get_provider)) -> ControlSuggestion:
    log.info("suggest-control via %s: %r", provider.name, request.text[:120])
    try:
        return provider.suggest_control(request.text, request.schema_context)
    except AiUnderstandingError as e:
        raise HTTPException(status_code=422, detail=str(e)) from e
    except AiProviderError as e:
        log.warning("provider failed: %s", e)
        raise HTTPException(status_code=502, detail=str(e)) from e


@app.post("/v1/suggest-quality-controls", response_model=QualityResponse, response_model_exclude_none=True)
def suggest_quality_controls(request: QualityRequest, provider: AiProvider = Depends(get_provider)) -> QualityResponse:
    try:
        return QualityResponse(suggestions=provider.suggest_quality_controls(request.schema_context, request.entity))
    except AiProviderError as e:
        raise HTTPException(status_code=502, detail=str(e)) from e


@app.post("/v1/prompt-preview")
def prompt_preview(request: SuggestControlRequest) -> dict[str, object]:
    """Transparency: exactly what a real model would receive - instructions, the data structure, and the expected response schema."""
    from app.providers.claude_provider import LlmControl

    return {
        "system": build_system_prompt(request.schema_context),
        "user": build_user_message(request.text),
        "responseSchema": LlmControl.model_json_schema(),
    }
