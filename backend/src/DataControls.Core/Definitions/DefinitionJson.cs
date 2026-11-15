using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DataControls.Core.Exceptions;

namespace DataControls.Core.Definitions;

/// <summary>Single place for reading and writing control definitions as JSON.</summary>
public static class DefinitionJson
{
    public static readonly JsonSerializerOptions Options = Configure(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    /// <summary>Applies the settings the definition contract needs (also used by the API).</summary>
    public static JsonSerializerOptions Configure(JsonSerializerOptions options)
    {
        // Lets "type" appear anywhere in the object, not only first (important for JSON coming from AI).
        options.AllowOutOfOrderMetadataProperties = true;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        // Keeps Hebrew readable in the database instead of א escapes.
        options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        options.PropertyNameCaseInsensitive = true;
        return options;
    }

    public static string Serialize(ControlDefinition definition) =>
        JsonSerializer.Serialize(definition, Options);

    public static ControlDefinition Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<ControlDefinition>(json, Options)
                   ?? throw new InvalidDefinitionFormatException("הגדרת הבקרה ריקה.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDefinitionFormatException($"מבנה הגדרת הבקרה אינו תקין: {ex.Message}", ex);
        }
    }
}
