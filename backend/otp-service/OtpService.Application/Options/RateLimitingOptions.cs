namespace OtpService.Application.Options;

/// <summary>Rate limits for security-sensitive OTP operations (per transaction).</summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";

    public int IssueLimitPerTransaction { get; set; } = 3;
    public int IssueWindowSeconds { get; set; } = 300;

    public int VerifyLimitPerTransaction { get; set; } = 10;
    public int VerifyWindowSeconds { get; set; } = 300;

    public TimeSpan IssueWindow => TimeSpan.FromSeconds(IssueWindowSeconds);
    public TimeSpan VerifyWindow => TimeSpan.FromSeconds(VerifyWindowSeconds);
}
