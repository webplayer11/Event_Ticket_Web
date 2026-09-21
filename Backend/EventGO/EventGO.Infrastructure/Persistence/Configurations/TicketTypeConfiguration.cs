using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class TicketTypeConfiguration
    : IEntityTypeConfiguration<TicketType>
{
    public void Configure(EntityTypeBuilder<TicketType> builder)
    {
        builder.ToTable("TicketTypes", table =>
        {
            table.HasCheckConstraint(
                "CK_TicketTypes_Price",
                "[Price] >= 0");

            table.HasCheckConstraint(
                "CK_TicketTypes_Quantities",
                "[TotalQuantity] >= 0 AND " +
                "[ReservedQuantity] >= 0 AND " +
                "[SoldQuantity] >= 0 AND " +
                "[SoldQuantity] <= [TotalQuantity] AND " +
                "[ReservedQuantity] <= [TotalQuantity] - [SoldQuantity]");

            table.HasCheckConstraint(
                "CK_TicketTypes_MaxQuantityPerOrder",
                "[MaxQuantityPerOrder] > 0");

            table.HasCheckConstraint(
                "CK_TicketTypes_SaleTimeRange",
                "[SaleEndsAt] > [SaleStartsAt]");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => new { x.EventId, x.Name })
            .IsUnique();

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}