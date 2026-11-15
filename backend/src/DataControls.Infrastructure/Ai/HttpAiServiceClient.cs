using System.Net.Http.Json;
using System.Text.Json;
using DataControls.Core.Ai;
using DataControls.Core.Definitions;
using DataControls.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace DataControls.Infrastructure.Ai;

public sealed class AiServiceOptions
{
    public const string Section = "AiService";
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Calls the Python AI service over HTTP. Knows only the contract (JSON), not the model.
/// Errors (service down, timeout, an answer not in the contract format) become an AiServiceException with a clear message.
/// </summary>
public sealed class HttpAiServiceClient(HttpClient http, ILogger<HttpAiServiceClient> logger) : IAiServiceClient
{
    private static readonly JsonSerializerOptions Json = DefinitionJson.Options;

    public Task<AiControlSuggestion> SuggestControlAsync(AiSuggestControlRequest request, CancellationToken ct = default) =>
        PostAsync<AiSuggestControlRequest, AiControlSuggestion>("v1/suggest-control", request, ct);

    public async Task<IReadOnlyList<AiControlSuggestion>> SuggestQualityControlsAsync(AiQualityRequest request, CancellationToken ct = default) =>
        (await PostAsync<AiQualityRequest, AiQualityResponse>("v1/suggest-quality-controls", request, ct)).Suggestions;

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(path, body, Json, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "AI service unreachable at {Path}", path);
            throw new AiServiceException("שירות ה-AI אינו זמין כרגע. אפשר להגדיר את הבקרה ידנית בעורך.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("AI service returned {Status}: {Body}", (int)response.StatusCode, errorBody);
                if ((int)response.StatusCode == 422)
                    throw new AiUnderstandingException(ReadDetail(errorBody) ?? "לא הצלחנו להבין את הבקשה. נסו לנסח אותה אחרת.");
                throw new AiServiceException("שירות ה-AI החזיר שגיאה. נסו שוב מאוחר יותר.");
            }

            try
            {
                return await response.Content.ReadFromJsonAsync<TResponse>(Json, ct)
                       ?? throw new AiServiceException("שירות ה-AI החזיר תשובה ריקה.");
            }
            catch (JsonException ex)
            {
                // The answer did not match the contract - it is not trusted and goes no further.
                logger.LogWarning(ex, "AI service response did not match the contract");
                throw new AiServiceException("תשובת שירות ה-AI אינה במבנה הצפוי.", ex);
            }
        }
    }

    /// <summary>FastAPI returns {"detail": "message"} for errors we raised, or {"detail": [...]} for contract errors.</summary>
    private static string? ReadDetail(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String
                ? detail.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
