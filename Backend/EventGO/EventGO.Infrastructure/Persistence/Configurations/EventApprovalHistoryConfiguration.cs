using EventGO.Domain.Entities;
using EventGO.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class EventApprovalHistoryConfiguration
    : IEntityTypeConfiguration<EventApprovalHistory>
{
    public void Configure(
        EntityTypeBuilder<EventApprovalHistory> builder)
    {
        builder.ToTable("EventApprovalHistories", table =>
        {
            table.HasCheckConstraint(
                "CK_EventApprovalHistories_FromStatus",
                "[FromStatus] IN (0, 1, 2, 3, 4, 5, 6)");

            table.HasCheckConstraint(
                "CK_EventApprovalHistories_ToStatus",
                "[ToStatus] IN (0, 1, 2, 3, 4, 5, 6)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FromStatus)
            .HasConversion<int>();

        builder.Property(x => x.ToStatus)
            .HasConversion<int>();

        builder.Property(x => x.Note)
            .HasMaxLength(2000)
            .IsRequired();

        builder.HasIndex(x => new { x.EventId, x.CreatedAt });

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.ActionByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}