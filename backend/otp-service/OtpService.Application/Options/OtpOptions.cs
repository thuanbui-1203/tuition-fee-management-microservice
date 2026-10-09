namespace OtpService.Application.Options;

/// <summary>OTP policy (configurable; security-sensitive values are never hard-coded).</summary>
public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    /// <summary>Number of digits in a generated OTP.</summary>
    public int Length { get; set; } = 6;

    /// <summary>OTP lifetime in seconds (default 5 minutes).</summary>
    public int TtlSeconds { get; set; } = 300;

    /// <summary>Maximum failed verification attempts before the OTP is locked.</summary>
    public int MaxAttempts { get; set; } = 5;
}
