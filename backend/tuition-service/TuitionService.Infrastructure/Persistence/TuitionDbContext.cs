using Microsoft.EntityFrameworkCore;
using TuitionService.Domain.Entities;

namespace TuitionService.Infrastructure.Persistence;

/// <summary>EF Core context for the tuition database (owns students and tuition_fees).</summary>
public sealed class TuitionDbContext : DbContext
{
    public TuitionDbContext(DbContextOptions<TuitionDbContext> options)
        : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<TuitionFee> TuitionFees => Set<TuitionFee>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TuitionDbContext).Assembly);
    }
}
