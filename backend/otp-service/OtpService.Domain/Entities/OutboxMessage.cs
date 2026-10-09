using OtpService.Domain.Enums;

namespace OtpService.Domain.Entities;

/// <summary>
/// Transactional outbox entry. Written in the same database transaction as the business
/// change (OTP issuance) so a crash between commit and publish can never lose the event.
/// The <see cref="MessageId"/> is stable across retries, enabling consumer deduplication.
/// </summary>
public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid messageId,
        string eventType,
        string routingKey,
        string recipient,
        Guid? transactionId,
        string? correlationId,
        string payload,
        DateTimeOffset createdAt)
    {
        MessageId = messageId;
        EventType = eventType;
        RoutingKey = routingKey;
        Recipient = recipient;
        TransactionId = transactionId;
        CorrelationId = correlationId;
        Payload = payload;
        Status = OutboxStatus.Pending;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }
    public Guid MessageId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public Guid? TransactionId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string Payload { get; private set; } = string.Empty;
    public OutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}
