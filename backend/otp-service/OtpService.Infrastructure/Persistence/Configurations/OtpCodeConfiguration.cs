using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OtpService.Domain.Entities;
using OtpService.Domain.Enums;

namespace OtpService.Infrastructure.Persistence.Configurations;

public sealed class OtpCodeConfiguration : IEntityTypeConfiguration<OtpCode>
{
    public void Configure(EntityTypeBuilder<OtpCode> builder)
    {
        builder.ToTable("otp_codes");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.TransactionId).IsRequired();

        builder.Property(x => x.CodeHash).HasMaxLength(200).IsRequired();

        builder.Property(x => x.ExpiresAt).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<OtpStatus>(v, true))
            .HasMaxLength(10)
            .IsRequired();
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_otp_codes_status", "\"status\" IN ('ACTIVE', 'USED', 'EXPIRED', 'LOCKED')"));

        builder.Property(x => x.AttemptCount).IsRequired();

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UsedAt);

        builder.Property(x => x.IdempotencyKey).HasMaxLength(64);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        // Only one ACTIVE OTP may exist per transaction (enforced at the database level).
        builder.HasIndex(x => x.TransactionId)
            .IsUnique()
            .HasFilter("\"status\" = 'ACTIVE'");

        builder.HasIndex(x => x.ExpiresAt);
    }
}
