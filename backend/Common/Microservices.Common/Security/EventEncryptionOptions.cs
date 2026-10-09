namespace Microservices.Common.Security;

/// <summary>
/// Shared key for authenticating-encrypting sensitive event payloads (e.g. the OTP inside an
/// outbox event). Supplied from configuration; the same value must be configured on the
/// producer (otp-service) and the consumer (notification-service).
/// </summary>
public sealed class EventEncryptionOptions
{
    public const string SectionName = "Security:EventEncryption";

    /// <summary>Base64-encoded 32-byte AES-256-GCM key.</summary>
    public string Key { get; set; } = string.Empty;

    public byte[] KeyBytes =>
        string.IsNullOrWhiteSpace(Key)
            ? throw new InvalidOperationException("Event encryption key is not configured.")
            : Convert.FromBase64String(Key);
}
