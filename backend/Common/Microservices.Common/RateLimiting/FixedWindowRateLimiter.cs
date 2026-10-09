namespace Microservices.Common.RateLimiting;

/// <summary>
/// Thread-safe fixed-window rate limiter. Suitable for a single instance; see
/// <see cref="IRateLimiter"/> for the multi-instance (Redis) recommendation.
/// Stale entries are pruned once the counter table exceeds a threshold so the dictionary
/// does not grow unbounded over the lifetime of a long-running service.
/// </summary>
public sealed class FixedWindowRateLimiter : IRateLimiter
{
    private const int PruneThreshold = 10_000;

    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<string, WindowCounter> _counters = new(StringComparer.Ordinal);

    public FixedWindowRateLimiter(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsAllowed(string key, int limitPerWindow, TimeSpan window)
    {
        if (limitPerWindow <= 0)
        {
            return false;
        }

        var now = _timeProvider.GetUtcNow();
        lock (_gate)
        {
            if (_counters.Count >= PruneThreshold)
            {
                PruneExpired(now);
            }

            if (!_counters.TryGetValue(key, out var counter) || now - counter.WindowStart >= counter.Window)
            {
                _counters[key] = new WindowCounter(now, window, 1);
                return true;
            }

            if (counter.Count >= limitPerWindow)
            {
                return false;
            }

            counter.Count++;
            return true;
        }
    }

    private void PruneExpired(DateTimeOffset now)
    {
        List<string>? expired = null;
        foreach (var (key, counter) in _counters)
        {
            if (now - counter.WindowStart >= counter.Window)
            {
                (expired ??= new List<string>()).Add(key);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var key in expired)
        {
            _counters.Remove(key);
        }
    }

    private sealed class WindowCounter
    {
        public WindowCounter(DateTimeOffset windowStart, TimeSpan window, int count)
        {
            WindowStart = windowStart;
            Window = window;
            Count = count;
        }

        public DateTimeOffset WindowStart { get; }
        public TimeSpan Window { get; }
        public int Count { get; set; }
    }
}
