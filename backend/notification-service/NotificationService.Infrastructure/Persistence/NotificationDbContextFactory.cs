using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Persistence;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>.</summary>
public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<NotificationDbContext>()
            .UseNpgsql("Host=localhost;Database=notification_db;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new NotificationDbContext(options);
    }
}
