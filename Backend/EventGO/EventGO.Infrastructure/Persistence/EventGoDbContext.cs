using EventGO.Domain.Entities;
using EventGO.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Persistence;

public class EventGoDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public EventGoDbContext(
        DbContextOptions<EventGoDbContext> options)
        : base(options)
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers
        => Set<OrganizationMember>();

    public DbSet<EventCategory> EventCategories => Set<EventCategory>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<EventDocument> EventDocuments => Set<EventDocument>();

    public DbSet<EventApprovalHistory> EventApprovalHistories
        => Set<EventApprovalHistory>();

    public DbSet<TicketType> TicketTypes => Set<TicketType>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<TicketReservation> TicketReservations
        => Set<TicketReservation>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<Ticket> Tickets => Set<Ticket>();

    public DbSet<EventStaffAssignment> EventStaffAssignments
        => Set<EventStaffAssignment>();

    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(EventGoDbContext).Assembly);
    }
}