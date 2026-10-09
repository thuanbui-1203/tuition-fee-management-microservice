namespace NotificationService.Application.Options;

/// <summary>Email delivery retry policy (bounded, exponential backoff).</summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notification";

    public int MaxAttempts { get; set; } = 3;
    public int InitialBackoffSeconds { get; set; } = 2;
    public int MaxBackoffSeconds { get; set; } = 30;
}
