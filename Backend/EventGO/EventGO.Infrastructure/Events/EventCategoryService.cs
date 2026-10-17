using EventGO.Application.Events;
using EventGO.Domain.Entities;
using EventGO.Domain.Enums;
using EventGO.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EventGO.Infrastructure.Events;

public sealed class EventCategoryService : IEventCategoryService
{
    private readonly EventGoDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public EventCategoryService(EventGoDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public Task<List<EventCategoryResponse>> GetAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default) =>
        _dbContext.EventCategories
            .AsNoTracking()
            .Where(x => includeInactive || x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new EventCategoryResponse
            {
                Id = x.Id,
                Name = x.Name,
                Slug = x.Slug,
                Description = x.Description,
                IsActive = x.IsActive
            })
            .ToListAsync(cancellationToken);

    public async Task<EventCategoryResult> CreateAsync(
        CreateEventCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var slug = string.IsNullOrWhiteSpace(request.Slug)
            ? SlugGenerator.Slugify(request.Name)
            : request.Slug.Trim();

        if (slug.Length == 0)
        {
            return new(null, EventCategoryError.InvalidSlug);
        }

        if (await SlugExistsAsync(slug, exceptId: null, cancellationToken))
        {
            return new(null, EventCategoryError.DuplicateSlug);
        }

        var category = new EventCategory
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description?.Trim() ?? string.Empty,
            IsActive = true
        };
        _dbContext.EventCategories.Add(category);

        return await SaveAsync(category, cancellationToken);
    }

    public async Task<EventCategoryResult> UpdateAsync(
        Guid categoryId,
        UpdateEventCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.EventCategories
            .SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return new(null, EventCategoryError.NotFound);
        }

        var slug = request.Slug.Trim();
        if (await SlugExistsAsync(slug, exceptId: categoryId, cancellationToken))
        {
            return new(null, EventCategoryError.DuplicateSlug);
        }

        category.Name = request.Name.Trim();
        category.Slug = slug;
        category.Description = request.Description?.Trim() ?? string.Empty;

        return await SaveAsync(category, cancellationToken);
    }

    public async Task<EventCategoryResult> SetStatusAsync(
        Guid categoryId,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var category = await _dbContext.EventCategories
            .SingleOrDefaultAsync(x => x.Id == categoryId, cancellationToken);

        if (category is null)
        {
            return new(null, EventCategoryError.NotFound);
        }

        if (category.IsActive == isActive)
        {
            // Idempotent: gửi lại cùng trạng thái không phải lỗi.
            return new(Map(category), EventCategoryError.None);
        }

        if (!isActive)
        {
            var now = _timeProvider.GetUtcNow();
            var activeEventCount = await _dbContext.Events.CountAsync(
                x => x.CategoryId == categoryId
                    && x.Status == EventStatus.Published
                    && x.EndsAt > now,
                cancellationToken);

            if (activeEventCount > 0)
            {
                return new(null, EventCategoryError.InUse, activeEventCount);
            }
        }

        category.IsActive = isActive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new(Map(category), EventCategoryError.None);
    }

    private Task<bool> SlugExistsAsync(
        string slug,
        Guid? exceptId,
        CancellationToken cancellationToken) =>
        _dbContext.EventCategories.AnyAsync(
            x => x.Slug == slug && (exceptId == null || x.Id != exceptId),
            cancellationToken);

    private async Task<EventCategoryResult> SaveAsync(
        EventCategory category,
        CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.IsUniqueViolation())
        {
            // Hai request cùng lúc dùng một slug: unique index chặn request sau.
            _dbContext.ChangeTracker.Clear();
            return new(null, EventCategoryError.DuplicateSlug);
        }

        return new(Map(category), EventCategoryError.None);
    }

    private static EventCategoryResponse Map(EventCategory category) => new()
    {
        Id = category.Id,
        Name = category.Name,
        Slug = category.Slug,
        Description = category.Description,
        IsActive = category.IsActive
    };
}
