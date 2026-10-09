namespace OtpService.Application.Dtos;

/// <summary>Input for an atomic OTP issuance (OTP + outbox write in one transaction).</summary>
public sealed record IssueOtpCommand(
    Guid TransactionId,
    string? IdempotencyKey,
    string CodeHash,
    DateTimeOffset Now,
    DateTimeOffset ExpiresAt,
    string Recipient,
    string CorrelationId,
    string EncryptedPayload);

/// <summary>Discriminated result of an atomic OTP issuance.</summary>
public abstract record IssueResult;

public sealed record IssueCreated(long OtpId) : IssueResult;

public sealed record IssueReplayed(long OtpId) : IssueResult;

public sealed record IssueIdempotencyConflict : IssueResult;

public sealed record OtpIssueOutcome(long OtpId, bool Replayed);

public sealed record OtpVerificationResult(bool Valid);
