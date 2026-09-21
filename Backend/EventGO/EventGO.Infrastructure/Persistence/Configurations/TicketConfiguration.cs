using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TicketCode).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(x => x.QrTokenHash).HasMaxLength(64).IsUnicode(false).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => x.TicketCode).IsUnique();
        builder.HasIndex(x => x.QrTokenHash).IsUnique();
        builder.HasOne<OrderItem>().WithMany().HasForeignKey(x => x.OrderItemId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
