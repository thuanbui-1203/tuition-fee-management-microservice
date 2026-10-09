using NotificationService.Domain.Enums;

namespace NotificationService.Domain.Entities;

/// <summary>
/// Audit log of every outbound email. <c>message_id</c> is the deduplication key: a unique
/// index guarantees the same event can never be emailed twice. Only metadata is stored —
/// email bodies are not persisted.
/// </summary>
public sealed class EmailLog
{
    private EmailLog()
    {
    }

    public EmailLog(string messageId, string eventType, string recipient, string subject, DateTimeOffset createdAt)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new ArgumentException("Message id is required.", nameof(messageId));
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ArgumentException("Event type is required.", nameof(eventType));
        }

        if (string.IsNullOrWhiteSpace(recipient))
        {
            throw new ArgumentException("Recipient is required.", nameof(recipient));
        }

        MessageId = messageId;
        EventType = eventType;
        Recipient = recipient;
        Subject = subject;
        Status = EmailStatus.Pending;
        AttemptCount = 0;
        CreatedAt = createdAt;
    }

    /// <summary>Reconstitution constructor for persistence and test fixtures.</summary>
    internal EmailLog(long id, string messageId, string eventType, string recipient, string subject, EmailStatus status, int attemptCount, string? providerMessageId, string? errorCode, string? errorMessage, DateTimeOffset createdAt, DateTimeOffset? sentAt)
    {
        Id = id;
        MessageId = messageId;
        EventType = eventType;
        Recipient = recipient;
        Subject = subject;
        Status = status;
        AttemptCount = attemptCount;
        ProviderMessageId = providerMessageId;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
        CreatedAt = createdAt;
        SentAt = sentAt;
    }

    public long Id { get; private set; }
    public string MessageId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public EmailStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public string? ProviderMessageId { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
}
