using System.Security.Cryptography;
using System.Text;
using Microservices.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microservices.Common.Api;

/// <summary>
/// Authenticates every <c>/internal/**</c> request with a shared service credential using a
/// constant-time comparison. Internal endpoints are never exposed publicly.
/// </summary>
public sealed class InternalApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<InternalApiKeyOptions> _options;
    private readonly ILogger<InternalApiKeyMiddleware> _logger;

    public InternalApiKeyMiddleware(RequestDelegate next, IOptions<InternalApiKeyOptions> options, ILogger<InternalApiKeyMiddleware> logger)
    {
        _next = next;
        _options = options;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/internal"))
        {
            await _next(context);
            return;
        }

        var configured = _options.Value.ApiKey;
        if (string.IsNullOrEmpty(configured))
        {
            _logger.LogError("Internal API key is not configured; rejecting internal request.");
            await WriteErrorAsync(context, Error.InternalError("Internal service authentication is not configured."));
            return;
        }

        var supplied = context.Request.Headers[_options.Value.HeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(supplied) || !FixedTimeEquals(configured, supplied))
        {
            await WriteErrorAsync(context, Error.Unauthenticated("Internal service authentication failed."));
            return;
        }

        await _next(context);
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(supplied);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static async Task WriteErrorAsync(HttpContext context, Error error)
    {
        context.Response.StatusCode = error.StatusCode;
        context.Response.ContentType = "application/json";
        var body = new ErrorResponse(error.Code, error.Message, DateTimeOffset.UtcNow, context.TraceIdentifier);
        await context.Response.WriteAsJsonAsync(body);
    }
}
