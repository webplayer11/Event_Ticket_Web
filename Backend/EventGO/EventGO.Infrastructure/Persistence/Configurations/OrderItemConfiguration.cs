using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration
    : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint(
                "CK_OrderItems_Quantity",
                "[Quantity] > 0");

            table.HasCheckConstraint(
                "CK_OrderItems_UnitPrice",
                "[UnitPrice] >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TicketTypeName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 2);

        builder.HasIndex(x => new { x.OrderId, x.TicketTypeId })
            .IsUnique();

        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<TicketType>()
            .WithMany()
            .HasForeignKey(x => x.TicketTypeId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}