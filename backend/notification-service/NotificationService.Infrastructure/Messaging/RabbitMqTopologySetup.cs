using Microservices.Common.Messaging;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>Declares the durable email exchange, queue, bindings and dead-letter topology (idempotent).</summary>
public static class RabbitMqTopologySetup
{
    public static void Declare(IModel channel)
    {
        channel.ExchangeDeclare(RabbitMqTopology.Exchange, ExchangeType.Topic, durable: true, autoDelete: false);
        channel.ExchangeDeclare(RabbitMqTopology.DeadLetterExchange, ExchangeType.Topic, durable: true, autoDelete: false);

        var args = new Dictionary<string, object>
        {
            ["x-dead-letter-exchange"] = RabbitMqTopology.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = RabbitMqTopology.DeadLetterRoutingKey
        };

        channel.QueueDeclare(RabbitMqTopology.EmailQueue, durable: true, exclusive: false, autoDelete: false, args);
        channel.QueueBind(RabbitMqTopology.EmailQueue, RabbitMqTopology.Exchange, RabbitMqTopology.OtpRequestedRoutingKey);
        channel.QueueBind(RabbitMqTopology.EmailQueue, RabbitMqTopology.Exchange, RabbitMqTopology.PaymentSucceededRoutingKey);

        channel.QueueDeclare(RabbitMqTopology.DeadLetterQueue, durable: true, exclusive: false, autoDelete: false);
        channel.QueueBind(RabbitMqTopology.DeadLetterQueue, RabbitMqTopology.DeadLetterExchange, RabbitMqTopology.DeadLetterRoutingKey);
    }
}
