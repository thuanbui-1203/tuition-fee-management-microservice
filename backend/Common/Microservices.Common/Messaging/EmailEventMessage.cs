using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microservices.Common.Messaging;

/// <summary>
/// Envelope for asynchronous email events. <see cref="MessageId"/> is the deduplication key
/// used by the notification consumer; <see cref="Payload"/> shape depends on
/// <see cref="EventType"/> (e.g. the OTP payload carries an AES-GCM-encrypted OTP).
/// </summary>
public sealed record EmailEventMessage(
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("correlationId")] string? CorrelationId,
    [property: JsonPropertyName("transactionId")] string? TransactionId,
    [property: JsonPropertyName("recipient")] string Recipient,
    [property: JsonPropertyName("payload")] JsonElement Payload);
