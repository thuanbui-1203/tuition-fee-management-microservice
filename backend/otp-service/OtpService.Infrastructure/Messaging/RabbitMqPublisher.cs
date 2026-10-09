using System.Text;
using System.Text.Json;
using Microservices.Common.Json;
using Microservices.Common.Messaging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace OtpService.Infrastructure.Messaging;

/// <summary>Publishes <see cref="EmailEventMessage"/> to the durable email exchange.</summary>
public interface IRabbitMqPublisher
{
    Task PublishAsync(EmailEventMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// RabbitMQ publisher with durable, persistent messages and publisher confirms. The
/// connection/channel are created lazily so the service stays up (and the outbox keeps
/// retrying) while RabbitMQ is temporarily unavailable.
/// </summary>
public sealed class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly ConnectionFactory _factory;
    private readonly object _gate = new();
    private IConnection? _connection;
    private IModel? _channel;

    public RabbitMqPublisher(IOptions<RabbitMqOptions> options)
    {
        var o = options.Value;
        _factory = new ConnectionFactory
        {
            HostName = o.HostName,
            Port = o.Port,
            UserName = o.UserName,
            Password = o.Password,
            VirtualHost = o.VirtualHost,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };
    }

    public Task PublishAsync(EmailEventMessage message, CancellationToken cancellationToken)
    {
        var channel = GetChannel();
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonDefaults.Options));

        var properties = channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = message.MessageId;
        properties.CorrelationId = message.CorrelationId;
        properties.ContentType = "application/json";

        channel.BasicPublish(RabbitMqTopology.Exchange, GetRoutingKey(message.EventType), false, properties, body);
        channel.WaitForConfirmsOrDie();
        return Task.CompletedTask;
    }

    private IModel GetChannel()
    {
        lock (_gate)
        {
            if (_channel is { IsOpen: true })
            {
                return _channel;
            }

            _channel?.Dispose();
            _connection?.Dispose();

            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            RabbitMqTopologySetup.Declare(_channel);
            _channel.ConfirmSelect();
            return _channel;
        }
    }

    private static string GetRoutingKey(string eventType) => eventType switch
    {
        EmailEventTypes.OtpRequested => RabbitMqTopology.OtpRequestedRoutingKey,
        EmailEventTypes.PaymentSucceeded => RabbitMqTopology.PaymentSucceededRoutingKey,
        _ => eventType
    };

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
