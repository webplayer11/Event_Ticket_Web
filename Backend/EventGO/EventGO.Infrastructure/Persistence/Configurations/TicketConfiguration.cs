using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", table =>
        {
            table.HasCheckConstraint(
                "CK_Tickets_Status",
                "[Status] IN (0, 1, 2)");
        });

        builder.HasKey(x => x.Id);

        // TKT-01.2: TicketCode — varchar(20), NOT NULL, UNIQUE
        builder.Property(x => x.TicketCode)
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        // TKT-01.3: QrTokenHash — varchar(64), NOT NULL, UNIQUE
        builder.Property(x => x.QrTokenHash)
            .HasMaxLength(64)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<int>();

        // TKT-01.4: RowVersion — SQL Server rowversion
        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        // Unique indexes
        builder.HasIndex(x => x.TicketCode)
            .IsUnique();

        builder.HasIndex(x => x.QrTokenHash)
            .IsUnique();

        // Query: tickets theo OrderItem
        builder.HasIndex(x => x.OrderItemId);

        // TKT-01.1: FK OrderItemId — bắt buộc, không cascade
        builder.HasOne(x => x.OrderItem)
            .WithMany(x => x.Tickets)
            .HasForeignKey(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
