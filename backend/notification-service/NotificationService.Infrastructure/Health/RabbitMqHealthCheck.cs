using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Health;

/// <summary>Readiness check verifying RabbitMQ reachability.</summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly ConnectionFactory _factory;

    public RabbitMqHealthCheck(ConnectionFactory factory) => _factory = factory;

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _factory.CreateConnection();
            return Task.FromResult(connection.IsOpen
                ? HealthCheckResult.Healthy("RabbitMQ reachable.")
                : HealthCheckResult.Unhealthy("RabbitMQ connection is not open."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ unreachable.", ex));
        }
    }
}
