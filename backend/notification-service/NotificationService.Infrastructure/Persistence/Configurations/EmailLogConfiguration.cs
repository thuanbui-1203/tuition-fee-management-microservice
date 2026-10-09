using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NotificationService.Domain.Entities;
using NotificationService.Domain.Enums;

namespace NotificationService.Infrastructure.Persistence.Configurations;

public sealed class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
{
    public void Configure(EntityTypeBuilder<EmailLog> builder)
    {
        builder.ToTable("email_logs");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.MessageId).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.MessageId).IsUnique();

        builder.Property(x => x.EventType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(254).IsRequired();
        builder.Property(x => x.Subject).HasMaxLength(200).IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<EmailStatus>(v, true))
            .HasMaxLength(10)
            .IsRequired();
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_email_logs_status", "\"status\" IN ('PENDING', 'SENDING', 'SENT', 'FAILED')"));

        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.ProviderMessageId).HasMaxLength(200);
        builder.Property(x => x.ErrorCode).HasMaxLength(50);
        builder.Property(x => x.ErrorMessage).HasColumnType("text");

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.SentAt);

        builder.HasIndex(x => x.Status);
    }
}
