using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Health;

/// <summary>Readiness check verifying connectivity to the notification PostgreSQL database.</summary>
public sealed class NotificationDatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public NotificationDatabaseHealthCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("notification_db reachable.")
                : HealthCheckResult.Unhealthy("Cannot connect to notification_db.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to notification_db.", ex);
        }
    }
}
