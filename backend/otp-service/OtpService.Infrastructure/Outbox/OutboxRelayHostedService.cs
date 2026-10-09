using System.Text.Json;
using Microservices.Common.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OtpService.Domain.Enums;
using OtpService.Infrastructure.Messaging;
using OtpService.Infrastructure.Persistence;

namespace OtpService.Infrastructure.Outbox;

/// <summary>
/// Relays PENDING outbox messages to RabbitMQ. Each message carries a stable
/// <see cref="EmailEventMessage.MessageId"/>, so a re-publish (for example after a crash
/// between publish and the SENT mark) is deduplicated by the consumer.
/// </summary>
public sealed class OutboxRelayHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayHostedService> _logger;
    private readonly TimeSpan _pollInterval;
    private readonly int _batchSize;

    public OutboxRelayHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxRelayHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollInterval = TimeSpan.FromSeconds(2);
        _batchSize = 100;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox relay iteration failed; will retry.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PublishPendingAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OtpDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IRabbitMqPublisher>();

        var pending = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == OutboxStatus.Pending)
            .OrderBy(m => m.Id)
            .Take(_batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            var envelope = new EmailEventMessage(
                message.MessageId.ToString(),
                message.EventType,
                message.CreatedAt,
                message.CorrelationId,
                message.TransactionId?.ToString(),
                message.Recipient,
                JsonSerializer.Deserialize<JsonElement>(message.Payload));

            try
            {
                await publisher.PublishAsync(envelope, cancellationToken);
                await db.OutboxMessages
                    .Where(m => m.Id == message.Id && m.Status == OutboxStatus.Pending)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(m => m.Status, OutboxStatus.Sent)
                        .SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to publish outbox message {MessageId}; will retry.", message.MessageId);
                await db.OutboxMessages
                    .Where(m => m.Id == message.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.AttemptCount, m => m.AttemptCount + 1), cancellationToken);
            }
        }
    }
}
