using OtpService.Domain.Enums;

namespace OtpService.Domain.Entities;

/// <summary>
/// A one-time code bound to a transaction. Only a hash of the code is stored; the plaintext
/// travels to the recipient exclusively through the encrypted outbox event.
/// </summary>
public sealed class OtpCode
{
    private OtpCode()
    {
    }

    public OtpCode(Guid transactionId, string codeHash, DateTimeOffset expiresAt, DateTimeOffset createdAt, string? idempotencyKey)
    {
        if (transactionId == Guid.Empty)
        {
            throw new ArgumentException("Transaction id is required.", nameof(transactionId));
        }

        if (string.IsNullOrWhiteSpace(codeHash))
        {
            throw new ArgumentException("Code hash is required.", nameof(codeHash));
        }

        if (expiresAt <= createdAt)
        {
            throw new ArgumentException("Expiry must be after creation.", nameof(expiresAt));
        }

        TransactionId = transactionId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
        Status = OtpStatus.Active;
        AttemptCount = 0;
        CreatedAt = createdAt;
        IdempotencyKey = idempotencyKey;
    }

    /// <summary>Reconstitution constructor for persistence and test fixtures.</summary>
    internal OtpCode(long id, Guid transactionId, string codeHash, DateTimeOffset expiresAt, OtpStatus status, int attemptCount, DateTimeOffset createdAt, DateTimeOffset? usedAt, string? idempotencyKey)
    {
        Id = id;
        TransactionId = transactionId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
        Status = status;
        AttemptCount = attemptCount;
        CreatedAt = createdAt;
        UsedAt = usedAt;
        IdempotencyKey = idempotencyKey;
    }

    public long Id { get; private set; }
    public Guid TransactionId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public OtpStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UsedAt { get; private set; }

    /// <summary>Issuance idempotency key supplied by the caller (unique, nullable).</summary>
    public string? IdempotencyKey { get; private set; }
}
