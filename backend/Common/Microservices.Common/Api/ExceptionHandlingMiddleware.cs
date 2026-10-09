using Microservices.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microservices.Common.Api;

/// <summary>
/// Last line of defence: converts unhandled exceptions into a sanitized
/// <c>500 INTERNAL_ERROR</c> response without leaking exception details, stack traces
/// or schema information to clients.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogInformation("Request cancelled by client for {Path}.", context.Request.Path);
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}.", context.Request.Method, context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            var body = new ErrorResponse(
                ErrorCodes.InternalError,
                "An unexpected error occurred.",
                DateTimeOffset.UtcNow,
                context.TraceIdentifier);
            await context.Response.WriteAsJsonAsync(body);
        }
    }
}
