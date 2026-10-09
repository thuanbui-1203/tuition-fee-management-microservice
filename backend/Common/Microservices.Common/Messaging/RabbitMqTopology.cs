namespace Microservices.Common.Messaging;

/// <summary>RabbitMQ topology shared by producers (otp-service) and consumers (notification-service).</summary>
public static class RabbitMqTopology
{
    public const string Exchange = "email.events";
    public const string OtpRequestedRoutingKey = "OtpRequested";
    public const string PaymentSucceededRoutingKey = "PaymentSucceeded";

    public const string EmailQueue = "notification.email";
    public const string DeadLetterExchange = "email.events.dlx";
    public const string DeadLetterQueue = "notification.email.dlq";
    public const string DeadLetterRoutingKey = "dead-letter";
}
