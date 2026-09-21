using EventGO.Domain.Entities;
using EventGO.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable("Reservations", table =>
        {
            table.HasCheckConstraint("CK_Reservations_ExpiresAt", "[ExpiresAt] > [CreatedAt]");
            table.HasCheckConstraint("CK_Reservations_Status", "[Status] IN (0, 1, 2, 3)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(Reservation.IdempotencyKeyMaxLength)
            .IsRequired();
        builder.Property(x => x.RequestHash)
            .HasMaxLength(Reservation.RequestHashLength)
            .IsUnicode(false)
            .IsFixedLength()
            .IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.Status, x.ExpiresAt });
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_Reservations_UserId_IdempotencyKey");
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.NoAction);
    }
}
