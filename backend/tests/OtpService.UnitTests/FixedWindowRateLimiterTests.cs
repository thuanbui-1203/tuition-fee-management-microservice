using FluentAssertions;
using Microservices.Common.RateLimiting;

namespace OtpService.UnitTests;

[Trait("Category", "Unit")]
public class FixedWindowRateLimiterTests
{
    [Fact]
    public void IsAllowed_AllowsUpToLimit_ThenRejects()
    {
        var limiter = new FixedWindowRateLimiter();

        for (var i = 0; i < 3; i++)
        {
            limiter.IsAllowed("key", 3, TimeSpan.FromMinutes(1)).Should().BeTrue();
        }

        limiter.IsAllowed("key", 3, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_ResetsAfterWindowElapses()
    {
        var time = new FakeTimeProvider();
        var limiter = new FixedWindowRateLimiter(time);

        limiter.IsAllowed("key", 1, TimeSpan.FromMinutes(1)).Should().BeTrue();
        limiter.IsAllowed("key", 1, TimeSpan.FromMinutes(1)).Should().BeFalse();

        time.UtcNow = time.UtcNow.AddMinutes(1);

        limiter.IsAllowed("key", 1, TimeSpan.FromMinutes(1)).Should().BeTrue();
    }

    [Fact]
    public void IsAllowed_ZeroLimit_AlwaysRejects()
    {
        var limiter = new FixedWindowRateLimiter();
        limiter.IsAllowed("key", 0, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    [Fact]
    public void IsAllowed_DistinctKeys_AreIndependent()
    {
        var limiter = new FixedWindowRateLimiter();
        limiter.IsAllowed("a", 1, TimeSpan.FromMinutes(1)).Should().BeTrue();
        limiter.IsAllowed("b", 1, TimeSpan.FromMinutes(1)).Should().BeTrue();
        limiter.IsAllowed("a", 1, TimeSpan.FromMinutes(1)).Should().BeFalse();
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
