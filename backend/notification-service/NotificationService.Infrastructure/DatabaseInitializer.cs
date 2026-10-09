using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure;

/// <summary>Applies EF Core migrations explicitly at startup (opt-in via configuration).</summary>
public static class DatabaseInitializer
{
    public static async Task MigrateAsync(IServiceProvider services, ILogger logger, CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Notification database migrated successfully.");
                return;
            }
            catch (Exception ex) when (attempt < 10 && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Notification database migration attempt {Attempt} failed; retrying.", attempt);
                await Task.Delay(TimeSpan.FromSeconds(Math.Min(attempt * 2, 15)), cancellationToken);
            }
        }
    }
}
