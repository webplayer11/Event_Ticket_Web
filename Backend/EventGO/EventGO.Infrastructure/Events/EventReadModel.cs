using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Events;

/// <summary>
/// Đọc sự kiện kèm thể loại, tổ chức, người tạo, loại vé
/// và kết quả duyệt gần nhất. Dùng chung cho EventService và EventReviewService.
/// </summary>
internal static class EventReadModel
{
    public static async Task<List<ManagedEventResponse>> LoadAsync(
        EventGoDbContext dbContext,
        IQueryable<Event> events,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from evt in events.AsNoTracking()
            join category in dbContext.EventCategories.AsNoTracking()
                on evt.CategoryId equals category.Id
            join organization in dbContext.Organizations.AsNoTracking()
                on evt.OrganizationId equals organization.Id
            join user in dbContext.Users.AsNoTracking()
                on evt.CreatedByUserId equals user.Id
            select new
            {
                Event = evt,
                CategoryName = category.Name,
                OrganizationName = organization.Name,
                user.FullName,
                user.Email
            }
        ).ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return new();
        }

        var eventIds = rows.Select(x => x.Event.Id).ToList();

        var ticketTypes = (await dbContext.TicketTypes
                .AsNoTracking()
                .Where(x => eventIds.Contains(x.EventId) && x.IsActive)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.EventId)
            .ToDictionary(x => x.Key, x => x.OrderBy(t => t.Price).ThenBy(t => t.Name).ToList());

        var lastReviews = (await dbContext.EventApprovalHistories
                .AsNoTracking()
                // Quyết định duyệt = mọi bước đi ra từ PendingApproval
                // (Approved/Rejected; dữ liệu cũ: duyệt thẳng sang Published).
                .Where(x => eventIds.Contains(x.EventId)
                    && x.FromStatus == EventStatus.PendingApproval)
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.EventId)
            .ToDictionary(x => x.Key, x => x.MaxBy(h => h.CreatedAt)!);

        return rows
            .Select(row =>
            {
                lastReviews.TryGetValue(row.Event.Id, out var review);
                ticketTypes.TryGetValue(row.Event.Id, out var tickets);

                return new ManagedEventResponse
                {
                    Id = row.Event.Id,
                    Title = row.Event.Title,
                    Slug = row.Event.Slug,
                    Description = row.Event.Description,
                    CategoryId = row.Event.CategoryId,
                    CategoryName = row.CategoryName,
                    OrganizationId = row.Event.OrganizationId,
                    OrganizationName = row.OrganizationName,
                    VenueName = row.Event.VenueName,
                    Address = row.Event.Address,
                    City = row.Event.City,
                    StartsAt = row.Event.StartsAt,
                    EndsAt = row.Event.EndsAt,
                    Status = row.Event.Status.ToString(),
                    CreatedAt = row.Event.CreatedAt,
                    PublishedAt = row.Event.PublishedAt,
                    TicketTypes = (tickets ?? new()).Select(MapTicketType).ToList(),
                    CreatedByUserId = row.Event.CreatedByUserId,
                    CreatedByName = row.FullName,
                    CreatedByEmail = row.Email ?? string.Empty,
                    LastReviewNote = review?.Note,
                    LastReviewedAt = review?.CreatedAt
                };
            })
            .ToList();
    }

    /// <summary>
    /// Bản công khai: chép riêng từng trường để không lộ thông tin
    /// người tạo hay ghi chú duyệt (serializer sẽ ghi cả thuộc tính
    /// của lớp con nếu trả thẳng ManagedEventResponse).
    /// </summary>
    public static EventResponse ToPublic(ManagedEventResponse source) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Slug = source.Slug,
        Description = source.Description,
        CategoryId = source.CategoryId,
        CategoryName = source.CategoryName,
        OrganizationId = source.OrganizationId,
        OrganizationName = source.OrganizationName,
        VenueName = source.VenueName,
        Address = source.Address,
        City = source.City,
        StartsAt = source.StartsAt,
        EndsAt = source.EndsAt,
        Status = source.Status,
        CreatedAt = source.CreatedAt,
        PublishedAt = source.PublishedAt,
        TicketTypes = source.TicketTypes
    };

    private static TicketTypeResponse MapTicketType(TicketType ticketType) => new()
    {
        Id = ticketType.Id,
        Name = ticketType.Name,
        Description = ticketType.Description,
        Price = ticketType.Price,
        Currency = ticketType.Currency,
        TotalQuantity = ticketType.TotalQuantity,
        AvailableQuantity = ticketType.AvailableQuantity,
        MaxQuantityPerOrder = ticketType.MaxQuantityPerOrder,
        SaleStartsAt = ticketType.SaleStartsAt,
        SaleEndsAt = ticketType.SaleEndsAt
    };
}
