using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class TicketReservationConfiguration
    : IEntityTypeConfiguration<TicketReservation>
{
    public void Configure(
        EntityTypeBuilder<TicketReservation> builder)
    {
        builder.ToTable("TicketReservations", table =>
        {
            table.HasCheckConstraint(
                "CK_TicketReservations_Quantity",
                "[Quantity] > 0");

            table.HasCheckConstraint(
                "CK_TicketReservations_ExpiresAt",
                "[ExpiresAt] > [CreatedAt]");

            table.HasCheckConstraint(
                "CK_TicketReservations_Status",
                "[Status] IN (0, 1, 2, 3)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .HasConversion<int>();

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.OrderItemId)
            .IsUnique();

        builder.HasIndex(x => new { x.Status, x.ExpiresAt });

        builder.HasOne<OrderItem>()
            .WithOne()
            .HasForeignKey<TicketReservation>(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}