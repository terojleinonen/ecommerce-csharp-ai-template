using ECommerce.Core.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ECommerce.Api.Http;

/// <summary>Maps domain exceptions to RFC 9457 problem details; hides internals for unexpected errors.</summary>
internal sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, code) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", "not_found"),
            ConflictException c => (StatusCodes.Status409Conflict, "Conflict", c.Code),
            DomainException d => (StatusCodes.Status400BadRequest, "Request could not be processed", d.Code),
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed", "invalid_credentials"),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized", "unauthorized"),
            AiUnavailableException => (StatusCodes.Status503ServiceUnavailable, "AI service unavailable", "ai_unavailable"),
            BadHttpRequestException b => (b.StatusCode, "Bad request", "bad_request"),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested => (499, "Client closed request", "cancelled"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", "internal_error"),
        };

        if (status >= 500 && status != StatusCodes.Status503ServiceUnavailable)
            LogUnhandled(logger, exception, httpContext.Request.Method, httpContext.Request.Path);

        // Only expose messages we wrote ourselves; never leak exception details for 5xx.
        var detail = status < 500 || exception is AiUnavailableException ? exception.Message : null;

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Extensions = { ["code"] = code },
            },
        });
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path}")]
    private static partial void LogUnhandled(ILogger logger, Exception exception, string method, string path);
}
