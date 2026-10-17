using EventGO.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Persistence;

internal static class TicketTypeLock
{
    // Call only inside a transaction. SQL Server inventory procedures update
    // these same rows; UPDLOCK also protects price snapshots and IsActive.
    public static Task<TicketType?> LoadAsync(EventGoDbContext db, Guid id, CancellationToken ct) =>
        (db.Database.IsSqlServer()
            ? db.TicketTypes.FromSqlInterpolated($"SELECT * FROM dbo.TicketTypes WITH (UPDLOCK, ROWLOCK) WHERE Id = {id}")
            : db.TicketTypes.Where(x => x.Id == id))
        .AsNoTracking().SingleOrDefaultAsync(ct);
}
