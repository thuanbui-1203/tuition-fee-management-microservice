using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TuitionService.Domain.Entities;
using TuitionService.Domain.Enums;

namespace TuitionService.Infrastructure.Persistence.Configurations;

public sealed class TuitionFeeConfiguration : IEntityTypeConfiguration<TuitionFee>
{
    public void Configure(EntityTypeBuilder<TuitionFee> builder)
    {
        builder.ToTable("tuition_fees");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Semester).HasMaxLength(20).IsRequired();

        builder.Property(x => x.Amount).HasPrecision(15, 2).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_tuition_fees_amount_positive", "\"amount\" > 0"));

        builder.Property(x => x.Status)
            .HasConversion<string>(
                v => v == FeeStatus.Paid ? "PAID" : "UNPAID",
                v => v == "PAID" ? FeeStatus.Paid : FeeStatus.Unpaid)
            .HasMaxLength(10)
            .IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_tuition_fees_status", "\"status\" IN ('UNPAID', 'PAID')"));

        builder.Property(x => x.PaidTransactionId);

        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasOne(x => x.Student)
            .WithMany(s => s.TuitionFees)
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.StudentId, x.Status });
    }
}
