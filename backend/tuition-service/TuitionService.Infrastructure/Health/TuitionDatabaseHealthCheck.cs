using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TuitionService.Infrastructure.Persistence;

namespace TuitionService.Infrastructure.Health;

/// <summary>Readiness check verifying connectivity to the tuition PostgreSQL database.</summary>
public sealed class TuitionDatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TuitionDatabaseHealthCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TuitionDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("tuition_db reachable.")
                : HealthCheckResult.Unhealthy("Cannot connect to tuition_db.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to tuition_db.", ex);
        }
    }
}
