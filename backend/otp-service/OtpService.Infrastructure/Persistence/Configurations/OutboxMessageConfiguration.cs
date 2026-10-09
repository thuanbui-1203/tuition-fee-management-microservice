using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OtpService.Domain.Entities;
using OtpService.Domain.Enums;

namespace OtpService.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.MessageId).IsRequired();
        builder.HasIndex(x => x.MessageId).IsUnique();

        builder.Property(x => x.EventType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.RoutingKey).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Recipient).HasMaxLength(254).IsRequired();
        builder.Property(x => x.TransactionId);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.Payload).HasColumnType("text").IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<OutboxStatus>(v, true))
            .HasMaxLength(10)
            .IsRequired();
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_outbox_messages_status", "\"status\" IN ('PENDING', 'SENT')"));

        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.Status).HasFilter("\"status\" = 'PENDING'");
    }
}
