using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using TuitionService.Infrastructure.Persistence;

namespace TuitionService.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the model without a running
/// application host. Migrations are generated explicitly and applied with
/// <c>Database.Migrate()</c> — never with EnsureCreated/EnsureDeleted in production.
/// </summary>
public sealed class TuitionDbContextFactory : IDesignTimeDbContextFactory<TuitionDbContext>
{
    public TuitionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TuitionDbContext>()
            .UseNpgsql("Host=localhost;Database=tuition_db;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TuitionDbContext(options);
    }
}
