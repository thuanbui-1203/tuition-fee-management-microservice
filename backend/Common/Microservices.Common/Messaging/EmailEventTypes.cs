namespace Microservices.Common.Messaging;

/// <summary>Canonical email event types carried over RabbitMQ.</summary>
public static class EmailEventTypes
{
    public const string OtpRequested = "OtpRequested";
    public const string PaymentSucceeded = "PaymentSucceeded";
}
