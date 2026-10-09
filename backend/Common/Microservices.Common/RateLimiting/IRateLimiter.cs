namespace Microservices.Common.RateLimiting;

/// <summary>
/// Minimal rate-limiting abstraction. The default implementation is a fixed-window
/// counter intended for a single service instance; for multi-instance deployments swap in a
/// Redis-backed implementation (documented in docs/security.md).
/// </summary>
public interface IRateLimiter
{
    /// <summary>
    /// Returns <c>true</c> when the operation identified by <paramref name="key"/> is allowed
    /// within the current window, otherwise <c>false</c>.
    /// </summary>
    bool IsAllowed(string key, int limitPerWindow, TimeSpan window);
}
