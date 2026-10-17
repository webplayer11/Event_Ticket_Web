using EventGO.Application.Authorization;
using EventGO.Application.TicketTypes;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.TicketTypes;

public sealed class TicketTypeService(EventGoDbContext db, IOrganizationAuthorizer authorizer,
    TimeProvider time) : ITicketTypeService
{
    public async Task<TicketTypeResult<List<TicketTypeResponse>>> GetAsync(Guid eventId, Guid? userId, CancellationToken cancellationToken = default)
    {
        var evt = await db.Events.AsNoTracking().SingleOrDefaultAsync(x => x.Id == eventId, cancellationToken);
        if (evt is null) return new(null, TicketTypeError.EventNotFound);
        var membership = userId is { } uid ? await authorizer.GetMembershipForEventAsync(eventId, uid, cancellationToken) : null;
        var manager = membership is not null && OrganizationPermissions.CanManageEvents(membership.Role);
        var active = await db.Organizations.AnyAsync(x => x.Id == evt.OrganizationId && x.IsActive, cancellationToken);
        if (!manager && (evt.Status != EventStatus.Published || !active)) return new(null, TicketTypeError.EventNotFound);
        var types = await db.TicketTypes.AsNoTracking().Where(x => x.EventId == eventId && (manager || x.IsActive))
            .OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return new(types.Select(Map).ToList());
    }

    public async Task<TicketTypeResult<TicketTypeResponse>> CreateAsync(Guid eventId, Guid userId, SaveTicketTypeRequest request, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize ticket creation/currency changes for the same event.
        var evt = await LoadEventAsync(eventId, cancellationToken);
        var error = await AuthorizeAsync(evt, userId, cancellationToken);
        if (error != TicketTypeError.None) return new(null, error);
        error = Validate(request, evt!);
        if (error != TicketTypeError.None) return new(null, error);
        if (await db.TicketTypes.AnyAsync(x => x.EventId == eventId && x.Currency != request.Currency.ToUpperInvariant(), cancellationToken))
            return new(null, TicketTypeError.CurrencyMismatch);
        var entity = new TicketType { EventId = eventId };
        Apply(entity, request);
        db.TicketTypes.Add(entity);
        return await SaveAsync(entity, tx, cancellationToken);
    }

    public async Task<TicketTypeResult<TicketTypeResponse>> UpdateAsync(Guid id, Guid userId, SaveTicketTypeRequest request, CancellationToken cancellationToken = default) =>
        await MutateAsync(id, userId, request, null, cancellationToken);

    public async Task<TicketTypeResult<TicketTypeResponse>> SetStatusAsync(Guid id, Guid userId, bool isActive, CancellationToken cancellationToken = default) =>
        await MutateAsync(id, userId, null, isActive, cancellationToken);

    private async Task<TicketTypeResult<TicketTypeResponse>> MutateAsync(Guid id, Guid userId, SaveTicketTypeRequest? request, bool? isActive, CancellationToken ct)
    {
        var eventId = await db.TicketTypes.Where(x => x.Id == id).Select(x => (Guid?)x.EventId).SingleOrDefaultAsync(ct);
        if (eventId is null) return new(null, TicketTypeError.NotFound);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var evt = await LoadEventAsync(eventId.Value, ct);
        var error = await AuthorizeAsync(evt, userId, ct);
        if (error != TicketTypeError.None) return new(null, error);
        var entity = await TicketTypeLock.LoadAsync(db, id, ct);
        if (entity is null) return new(null, TicketTypeError.NotFound);
        if (!Editable(evt!)) return new(null, TicketTypeError.EventNotEditable);
        if (request is not null)
        {
            error = Validate(request, evt!);
            if (error != TicketTypeError.None) return new(null, error);
            if (request.TotalQuantity < (long)entity.SoldQuantity + entity.ReservedQuantity)
                return new(null, TicketTypeError.InventoryCommitted);
            if (entity.Price != request.Price || entity.Currency != request.Currency.ToUpperInvariant())
            {
                if (time.GetUtcNow() >= entity.SaleStartsAt || time.GetUtcNow() >= request.SaleStartsAt
                    || entity.SoldQuantity + (long)entity.ReservedQuantity > 0
                    || await db.ReservationItems.AnyAsync(x => x.TicketTypeId == id, ct)
                    || await db.OrderItems.AnyAsync(x => x.TicketTypeId == id, ct))
                    return new(null, TicketTypeError.PriceLocked);
            }
            if (await db.TicketTypes.AnyAsync(x => x.EventId == entity.EventId && x.Id != id && x.Currency != request.Currency.ToUpperInvariant(), ct))
                return new(null, TicketTypeError.CurrencyMismatch);
            db.Attach(entity);
            Apply(entity, request);
        }
        else
        {
            if (isActive == true && (entity.SaleEndsAt <= time.GetUtcNow() || entity.SaleEndsAt > evt!.StartsAt))
                return new(null, TicketTypeError.SaleWindowClosed);
            db.Attach(entity);
            entity.IsActive = isActive!.Value;
        }
        return await SaveAsync(entity, tx, ct);
    }

    private Task<Event?> LoadEventAsync(Guid id, CancellationToken ct) =>
        (db.Database.IsSqlServer()
            ? db.Events.FromSqlInterpolated($"SELECT * FROM dbo.Events WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
            : db.Events.Where(x => x.Id == id)).AsNoTracking().SingleOrDefaultAsync(ct);

    private async Task<TicketTypeError> AuthorizeAsync(Event? evt, Guid userId, CancellationToken ct)
    {
        if (evt is null) return TicketTypeError.EventNotFound;
        var member = await authorizer.GetMembershipForEventAsync(evt.Id, userId, ct);
        return member is null ? TicketTypeError.EventNotFound :
            !OrganizationPermissions.CanManageEvents(member.Role) ? TicketTypeError.Forbidden : TicketTypeError.None;
    }

    private bool Editable(Event evt) => evt.StartsAt > time.GetUtcNow() && evt.Status is
        EventStatus.Draft or EventStatus.Rejected or EventStatus.Approved or EventStatus.Published;

    private TicketTypeError Validate(SaveTicketTypeRequest r, Event evt)
    {
        if (!Editable(evt)) return TicketTypeError.EventNotEditable;
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 150 || r.Description is null || r.Description.Length > 2000
            || r.Price < 0 || r.Price > 9999999999999999.99m || decimal.Round(r.Price, 2) != r.Price
            || r.TotalQuantity <= 0 || r.MaxQuantityPerOrder <= 0 || r.MaxQuantityPerOrder > r.TotalQuantity
            || r.Currency is null || r.Currency.Length != 3 || !r.Currency.All(char.IsAsciiLetter)
            || r.SaleStartsAt == default || r.SaleEndsAt <= r.SaleStartsAt || r.SaleEndsAt > evt.StartsAt)
            return TicketTypeError.Invalid;
        return TicketTypeError.None;
    }

    private async Task<TicketTypeResult<TicketTypeResponse>> SaveAsync(TicketType entity,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new(Map(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return new(null, TicketTypeError.Conflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 }
            || ex.InnerException?.GetType().Name == "SqliteException" && ex.InnerException.Message.Contains("UNIQUE constraint failed"))
        {
            await tx.RollbackAsync(ct);
            db.ChangeTracker.Clear();
            return new(null, TicketTypeError.Conflict);
        }
    }

    private static void Apply(TicketType e, SaveTicketTypeRequest r)
    {
        e.Name = r.Name.Trim(); e.Description = r.Description.Trim(); e.Price = r.Price;
        e.Currency = r.Currency.ToUpperInvariant(); e.TotalQuantity = r.TotalQuantity;
        e.MaxQuantityPerOrder = r.MaxQuantityPerOrder; e.SaleStartsAt = r.SaleStartsAt; e.SaleEndsAt = r.SaleEndsAt;
    }

    private static TicketTypeResponse Map(TicketType e) => new(e.Id, e.EventId, e.Name, e.Description,
        e.Price, e.Currency, e.TotalQuantity, e.AvailableQuantity, e.MaxQuantityPerOrder,
        e.SaleStartsAt, e.SaleEndsAt, e.IsActive);
}
