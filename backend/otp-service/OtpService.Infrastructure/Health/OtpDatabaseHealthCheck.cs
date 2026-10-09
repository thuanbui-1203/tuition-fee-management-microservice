using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OtpService.Infrastructure.Persistence;

namespace OtpService.Infrastructure.Health;

/// <summary>Readiness check verifying connectivity to the OTP PostgreSQL database.</summary>
public sealed class OtpDatabaseHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public OtpDatabaseHealthCheck(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OtpDbContext>();
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("otp_db reachable.")
                : HealthCheckResult.Unhealthy("Cannot connect to otp_db.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot connect to otp_db.", ex);
        }
    }
}
