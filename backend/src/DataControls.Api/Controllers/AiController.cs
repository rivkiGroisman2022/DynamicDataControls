using DataControls.Core.Ai;
using Microsoft.AspNetCore.Mvc;

namespace DataControls.Api.Controllers;

/// <summary>Stage B: defining controls in free text with the help of AI. The AI only suggests - running only after user approval.</summary>
[ApiController]
[Route("api/ai")]
[Produces("application/json")]
public sealed class AiController(AiAssistantService assistant) : ControllerBase
{
    /// <summary>Free text → a validated control suggestion + a description in words for approval. Does not save and does not run.</summary>
    [HttpPost("suggest-control")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public Task<AiSuggestionDto> SuggestControl(SuggestControlRequestDto request, CancellationToken ct) =>
        assistant.SuggestControlAsync(request.Text, ct);

    /// <summary>Advanced task: proactive data-quality control suggestions based on analyzing the data structure.</summary>
    [HttpPost("quality-suggestions")]
    public Task<IReadOnlyList<AiSuggestionDto>> QualitySuggestions(QualitySuggestionsRequestDto request, CancellationToken ct) =>
        assistant.SuggestQualityControlsAsync(request.Entity, ct);

    /// <summary>Transparency: exactly what the AI model receives about the data structure.</summary>
    [HttpGet("schema-context")]
    public Task<AiSchemaContext> SchemaContext(CancellationToken ct) => assistant.GetContextAsync(ct);
}
