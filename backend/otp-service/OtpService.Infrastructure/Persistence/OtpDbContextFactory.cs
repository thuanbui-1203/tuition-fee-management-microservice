using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using OtpService.Infrastructure.Persistence;

namespace OtpService.Infrastructure.Persistence;

/// <summary>Design-time factory for <c>dotnet ef migrations</c>.</summary>
public sealed class OtpDbContextFactory : IDesignTimeDbContextFactory<OtpDbContext>
{
    public OtpDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<OtpDbContext>()
            .UseNpgsql("Host=localhost;Database=otp_db;Username=postgres;Password=postgres")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new OtpDbContext(options);
    }
}
