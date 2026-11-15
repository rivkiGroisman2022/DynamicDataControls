using DataControls.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DataControls.Api.Infrastructure;

/// <summary>
/// Single place that turns exceptions into HTTP responses in the standard ProblemDetails format (RFC 9457).
/// The controllers contain no try/catch: they assume success, and every error comes here.
/// - Expected error (DataControlsException) → matching status + a clear Hebrew message.
/// - Unexpected error (bug) → 500 with a generic message; the details go to the log only (not exposed to the client).
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetails)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "לא נמצא"),
            ConflictException => (StatusCodes.Status409Conflict, "התנגשות"),
            DefinitionValidationException or InvalidDefinitionFormatException => (StatusCodes.Status400BadRequest, "הבקרה אינה תקינה"),
            QueryExecutionException => (StatusCodes.Status422UnprocessableEntity, "הרצת הבקרה נכשלה"),
            AiUnderstandingException => (StatusCodes.Status422UnprocessableEntity, "הבקשה לא הובנה"),
            AiServiceException => (StatusCodes.Status503ServiceUnavailable, "שירות ה-AI אינו זמין"),
            MetadataConfigurationException => (StatusCodes.Status500InternalServerError, "שגיאת הגדרות מערכת"),
            OperationCanceledException => (499, "הבקשה בוטלה"),
            _ => (StatusCodes.Status500InternalServerError, "שגיאה לא צפויה")
        };

        if (status >= 500) logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);
        else logger.LogInformation("Request failed with {Status}: {Message}", status, exception.Message);

        var details = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception is DataControlsException ? exception.Message : "אירעה שגיאה בשרת. אם התקלה חוזרת, יש לפנות לתמיכה.",
        };

        if (exception is DefinitionValidationException validation)
            details.Extensions["errors"] = validation.Errors;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = details,
            Exception = exception
        });
    }
}
