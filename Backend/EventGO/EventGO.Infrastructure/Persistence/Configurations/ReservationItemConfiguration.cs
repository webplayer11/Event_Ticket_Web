using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class ReservationItemConfiguration : IEntityTypeConfiguration<ReservationItem>
{
    public void Configure(EntityTypeBuilder<ReservationItem> builder)
    {
        builder.ToTable("ReservationItems", table =>
        {
            table.HasCheckConstraint("CK_ReservationItems_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_ReservationItems_UnitPriceSnapshot", "[UnitPriceSnapshot] >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UnitPriceSnapshot).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.ReservationId, x.TicketTypeId }).IsUnique();
        builder.HasOne<Reservation>().WithMany().HasForeignKey(x => x.ReservationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<TicketType>().WithMany().HasForeignKey(x => x.TicketTypeId).OnDelete(DeleteBehavior.NoAction);
    }
}
