using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionService.Domain.Entities;

namespace TuitionService.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Mssv).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Mssv).IsUnique();

        builder.Property(x => x.FullName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ClassName).HasMaxLength(50);
        builder.Property(x => x.Faculty).HasMaxLength(100);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
