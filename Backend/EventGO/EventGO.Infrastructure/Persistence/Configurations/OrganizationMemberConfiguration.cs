using EventGO.Domain.Entities;
using EventGO.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventGO.Infrastructure.Persistence.Configurations;

public class OrganizationMemberConfiguration
    : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers", table =>
        {
            table.HasCheckConstraint(
                "CK_OrganizationMembers_Role",
                "[Role] IN (1, 2, 3)");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role)
            .HasConversion<int>();

        builder.HasIndex(x => new { x.OrganizationId, x.UserId })
            .IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}