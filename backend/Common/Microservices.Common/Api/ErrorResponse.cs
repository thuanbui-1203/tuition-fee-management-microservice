namespace Microservices.Common.Api;

/// <summary>
/// The unified error response body shared by every service.
/// </summary>
public sealed record ErrorResponse(string Code, string Message, DateTimeOffset Timestamp, string TraceId);
