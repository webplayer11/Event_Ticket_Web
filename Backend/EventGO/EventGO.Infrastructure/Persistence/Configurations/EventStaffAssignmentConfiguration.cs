using EventGO.Domain.Entities;
using EventGO.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class EventStaffAssignmentConfiguration
    : IEntityTypeConfiguration<EventStaffAssignment>
{
    public void Configure(
        EntityTypeBuilder<EventStaffAssignment> builder)
    {
        builder.ToTable("EventStaffAssignments");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new
        {
            x.EventId,
            x.OrganizationMemberId
        }).IsUnique();

        builder.HasOne<Event>()
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<OrganizationMember>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationMemberId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.AssignedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}