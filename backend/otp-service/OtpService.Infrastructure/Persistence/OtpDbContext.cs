using Microsoft.EntityFrameworkCore;
using OtpService.Domain.Entities;

namespace OtpService.Infrastructure.Persistence;

/// <summary>EF Core context for the OTP database (owns otp_codes and the outbox).</summary>
public sealed class OtpDbContext : DbContext
{
    public OtpDbContext(DbContextOptions<OtpDbContext> options)
        : base(options)
    {
    }

    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OtpDbContext).Assembly);
    }
}
