using Microsoft.AspNetCore.Diagnostics;
using System.ComponentModel.DataAnnotations;

namespace CopilotNotepad.ApiService.Middleware;

/// <summary>
/// Global exception handler for the API
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "An unhandled exception occurred");

        var response = exception switch
        {
            ValidationException validationEx => new
            {
                title = "Validation Error",
                status = StatusCodes.Status400BadRequest,
                detail = validationEx.Message
            },
            UnauthorizedAccessException => new
            {
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                detail = "You are not authorized to access this resource"
            },
            ArgumentException argumentEx => new
            {
                title = "Bad Request",
                status = StatusCodes.Status400BadRequest,
                detail = argumentEx.Message
            },
            _ => new
            {
                title = "Internal Server Error",
                status = StatusCodes.Status500InternalServerError,
                detail = "An unexpected error occurred"
            }
        };

        httpContext.Response.StatusCode = response.status;
        httpContext.Response.ContentType = "application/json";

        await httpContext.Response.WriteAsJsonAsync(response, cancellationToken);

        return true;
    }
}