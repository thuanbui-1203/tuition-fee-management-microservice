using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Microservices.Common.Api;

/// <summary>
/// Propagates a correlation id across services. Reads <c>X-Correlation-Id</c> (falls back to
/// <c>X-Trace-Id</c>), reuses it as <see cref="HttpContext.TraceIdentifier"/>, echoes it on the
/// response, and enriches every log entry with it.
/// </summary>
public sealed class CorrelationMiddleware
{
    public const string CorrelationHeader = "X-Correlation-Id";
    public const string TraceHeader = "X-Trace-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationMiddleware> _logger;

    public CorrelationMiddleware(RequestDelegate next, ILogger<CorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationHeader].FirstOrDefault()
                            ?? context.Request.Headers[TraceHeader].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;
        context.Response.Headers[TraceHeader] = correlationId;

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["traceId"] = correlationId,
            ["correlationId"] = correlationId
        });

        await _next(context);
    }
}
