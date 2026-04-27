using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CareerForge.Api.Common;

/// <summary>
/// Maps unhandled exceptions to RFC 7807 problem responses. In Development the original
/// message and stack trace are surfaced for debugging; in other environments only a generic title is returned.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment env)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception on {Path}", httpContext.Request.Path);

        var (status, title) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            KeyNotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad request"),
            NotSupportedException => (StatusCodes.Status400BadRequest, "Bad request"),
            HttpRequestException => (StatusCodes.Status502BadGateway, "Upstream service error"),
            TaskCanceledException => (StatusCodes.Status504GatewayTimeout, "Upstream timeout"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred"),
        };

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = env.IsDevelopment() ? exception.Message : null,
            Instance = httpContext.Request.Path,
            Type = $"https://httpstatuses.com/{status}",
        };
        if (env.IsDevelopment())
            problem.Extensions["stackTrace"] = exception.StackTrace;

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
        return true;
    }
}
