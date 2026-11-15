using DataControls.Core.Validation;

namespace DataControls.Core.Exceptions;

/// <summary>
/// Base for all expected errors in the system. The message is meant for the business user (Hebrew)
/// and the API turns it into a ProblemDetails response with a matching status code.
/// Unexpected errors (bugs) do not inherit from it and get a generic 500.
/// </summary>
public abstract class DataControlsException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class NotFoundException(string message) : DataControlsException(message);

public sealed class ConflictException(string message) : DataControlsException(message);

public sealed class InvalidDefinitionFormatException(string message, Exception? inner = null)
    : DataControlsException(message, inner);

public sealed class DefinitionValidationException(IReadOnlyList<ValidationError> errors)
    : DataControlsException("הגדרת הבקרה אינה תקינה.")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}

/// <summary>Running the query failed (timeout, permissions, database unavailable).</summary>
public sealed class QueryExecutionException(string message, Exception? inner = null)
    : DataControlsException(message, inner);

/// <summary>The metadata layer is misconfigured (e.g. an invalid column name). An admin problem, not a user one.</summary>
public sealed class MetadataConfigurationException(string message) : DataControlsException(message);

/// <summary>The AI service is up, but did not manage to understand the request (e.g. no conditions in the text). The message comes from the model.</summary>
public sealed class AiUnderstandingException(string message) : DataControlsException(message);

/// <summary>The external AI service is unavailable or returned an invalid answer.</summary>
public sealed class AiServiceException(string message, Exception? inner = null)
    : DataControlsException(message, inner);
