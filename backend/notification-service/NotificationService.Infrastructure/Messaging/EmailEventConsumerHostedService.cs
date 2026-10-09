using System.Text.Json;
using Microservices.Common.Json;
using Microservices.Common.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationService.Application.Services;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ consumer for email events. Uses a durable queue with a dead-letter exchange,
/// manual acks, prefetch=1, and idempotent processing (duplicate message ids are dropped).
/// Reconnects automatically while RabbitMQ is unavailable.
/// </summary>
public sealed class EmailEventConsumerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConnectionFactory _factory;
    private readonly ILogger<EmailEventConsumerHostedService> _logger;

    public EmailEventConsumerHostedService(
        IServiceScopeFactory scopeFactory,
        ConnectionFactory factory,
        ILogger<EmailEventConsumerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _factory = factory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var connection = _factory.CreateConnection();
                using var channel = connection.CreateModel();
                RabbitMqTopologySetup.Declare(channel);
                channel.BasicQos(0, 1, false);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (_, ea) => await HandleAsync(channel, ea, stoppingToken);
                channel.BasicConsume(RabbitMqTopology.EmailQueue, autoAck: false, consumer);

                _logger.LogInformation("Email event consumer started on queue {Queue}.", RabbitMqTopology.EmailQueue);

                var shutdown = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                connection.ConnectionShutdown += (_, _) => shutdown.TrySetResult(true);
                channel.ModelShutdown += (_, _) => shutdown.TrySetResult(true);

                await Task.WhenAny(shutdown.Task, WaitForCancellationAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ consumer error; reconnecting.");
            }

            if (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleAsync(IModel channel, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            EmailEventMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<EmailEventMessage>(ea.Body.ToArray(), JsonDefaults.Options);
            }
            catch (JsonException)
            {
                message = null;
            }

            if (message is null || string.IsNullOrWhiteSpace(message.MessageId))
            {
                _logger.LogWarning("Malformed email event received; dead-lettering.");
                channel.BasicNack(ea.DeliveryTag, false, false);
                return;
            }

            using var scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<EmailProcessingService>();
            var outcome = await processor.ProcessAsync(message, cancellationToken);

            if (outcome == ProcessOutcome.Completed)
            {
                channel.BasicAck(ea.DeliveryTag, false);
            }
            else
            {
                channel.BasicNack(ea.DeliveryTag, false, false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Do not ack: the message will be redelivered after a graceful restart.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error handling email event; dead-lettering.");
            channel.BasicNack(ea.DeliveryTag, false, false);
        }
    }

    private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var registration = cancellationToken.Register(() => tcs.TrySetResult(true));
        await tcs.Task;
    }
}
