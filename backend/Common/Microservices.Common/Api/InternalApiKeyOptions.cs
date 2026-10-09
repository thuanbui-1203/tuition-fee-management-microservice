namespace Microservices.Common.Api;

/// <summary>
/// Configuration for service-to-service authentication of internal endpoints.
/// In production this should be replaced by mTLS or short-lived service JWTs; a shared
/// API key is the documented dev/self-hosted default (see docs/security.md).
/// </summary>
public sealed class InternalApiKeyOptions
{
    public const string SectionName = "Security:InternalApi";

    /// <summary>Header that carries the service credential.</summary>
    public string HeaderName { get; set; } = "X-Internal-Api-Key";

    /// <summary>The shared secret. Always supplied from configuration/environment.</summary>
    public string ApiKey { get; set; } = string.Empty;
}
