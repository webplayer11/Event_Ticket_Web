using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class CheckInConfiguration : IEntityTypeConfiguration<CheckIn>
{
    public void Configure(EntityTypeBuilder<CheckIn> builder)
    {
        builder.ToTable("CheckIns");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.GateName)
            .HasMaxLength(100);

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.TicketId)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.EventStaffAssignmentId,
            x.CheckedInAt
        });

        builder.HasOne<Ticket>()
            .WithOne()
            .HasForeignKey<CheckIn>(x => x.TicketId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<EventStaffAssignment>()
            .WithMany()
            .HasForeignKey(x => x.EventStaffAssignmentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}