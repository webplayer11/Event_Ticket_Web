using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments", table =>
        {
            table.HasCheckConstraint(
                "CK_Payments_Amount",
                "[Amount] >= 0");

            table.HasCheckConstraint(
                "CK_Payments_Status",
                "[Status] IN (0, 1, 2, 3, 4)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .HasMaxLength(50)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.PaymentReference)
            .HasMaxLength(100)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.ProviderTransactionId)
            .HasMaxLength(150)
            .IsUnicode(false);

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>();

        builder.Property(x => x.FailureReason)
            .HasMaxLength(2000);

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.PaymentReference)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.Provider,
            x.ProviderTransactionId
        })
            .IsUnique()
            .HasFilter("[ProviderTransactionId] IS NOT NULL");

        builder.HasIndex(x => new { x.OrderId, x.Status });

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}