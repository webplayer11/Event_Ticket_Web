using System.ComponentModel.DataAnnotations;
using EventGO.Domain.Enums;

namespace EventGO.Application.Events;

public enum EventError
{
    None,
    // Sự kiện/tổ chức không tồn tại hoặc người gọi không thuộc tổ chức.
    NotFound,
    Forbidden,
    CategoryNotFound,
    // Dữ liệu sai quy tắc nghiệp vụ (thời gian, loại vé...). Xem Errors.
    Invalid,
    // Trạng thái hiện tại không cho phép thao tác (vd: sửa sự kiện đã công khai).
    InvalidStatus,
    // Dữ liệu vừa bị người khác thay đổi.
    Conflict,
    // Đã có vé được giữ chỗ/bán: chưa có chính sách hoàn tiền nên không được hủy.
    TicketsSold
}

public sealed record EventResult<T>(
    T? Data,
    EventError Error,
    IReadOnlyList<string>? Errors = null)
    where T : class;

/// <summary>
/// Luồng trạng thái sự kiện. Mọi thao tác đổi trạng thái đều kiểm tra ở đây.
/// <code>
/// Draft → PendingApproval → Approved → Published → Completed
///                      ↘ Rejected → (sửa, gửi lại) PendingApproval
/// Draft/Approved/Published → Cancelled
/// </code>
/// </summary>
public static class EventStatusTransitions
{
    private static readonly Dictionary<EventStatus, EventStatus[]> Allowed = new()
    {
        [EventStatus.Draft] = [EventStatus.PendingApproval, EventStatus.Cancelled],
        [EventStatus.PendingApproval] = [EventStatus.Approved, EventStatus.Rejected],
        [EventStatus.Rejected] = [EventStatus.PendingApproval],
        [EventStatus.Approved] = [EventStatus.Published, EventStatus.Cancelled],
        [EventStatus.Published] = [EventStatus.Completed, EventStatus.Cancelled]
    };

    public static bool CanTransition(EventStatus from, EventStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Chỉ sửa được khi sự kiện chưa gửi duyệt hoặc bị từ chối. Sau khi duyệt
    /// chưa có chính sách sửa (vé có thể đã bán), nên không cho sửa.
    /// </summary>
    public static bool IsEditable(EventStatus status) =>
        status is EventStatus.Draft or EventStatus.Rejected;
}

public class TicketTypeRequest
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Range(typeof(decimal), "0", "1000000000")]
    public decimal Price { get; set; }

    [Range(1, 1_000_000)]
    public int TotalQuantity { get; set; }

    [Range(1, 100)]
    public int MaxQuantityPerOrder { get; set; } = 10;

    public DateTimeOffset SaleStartsAt { get; set; }

    public DateTimeOffset SaleEndsAt { get; set; }
}

/// <summary>
/// Thông tin sự kiện dùng cho tạo nháp và sửa.
/// </summary>
public class EventDetailsRequest
{
    [Required]
    [StringLength(250, MinimumLength = 2)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string VenueName { get; set; } = string.Empty;

    [Required]
    [StringLength(500, MinimumLength = 1)]
    public string Address { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string City { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    /// <summary>
    /// null: giữ nguyên loại vé hiện có (quản lý riêng qua API ticket-types).
    /// Có giá trị: thay toàn bộ loại vé. Khi gửi duyệt phải có ít nhất một loại vé.
    /// </summary>
    [MaxLength(20)]
    public List<TicketTypeRequest>? TicketTypes { get; set; }
}

/// <summary>
/// Body của <c>POST /api/events</c>: tạo và gửi duyệt ngay.
/// </summary>
public class SaveEventRequest : EventDetailsRequest
{
    /// <summary>
    /// Tổ chức sở hữu sự kiện. Bỏ trống: dùng tổ chức cá nhân của người tạo
    /// (tự tạo ở lần đầu). Có giá trị: người tạo phải là Owner/Manager.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    // Tên/thông tin ban tổ chức: dùng để đặt tên tổ chức cá nhân
    // khi hệ thống tự tạo. Bỏ qua nếu đã có tổ chức.
    [StringLength(200)]
    public string? OrganizerName { get; set; }

    [StringLength(4000)]
    public string? OrganizerInfo { get; set; }
}

public class ReviewEventRequest
{
    // Ghi chú khi duyệt (không bắt buộc) hoặc lý do từ chối (bắt buộc).
    [StringLength(2000)]
    public string? Note { get; set; }
}

public class CancelEventRequest
{
    [StringLength(2000)]
    public string? Reason { get; set; }
}

/// <summary>
/// Bộ lọc danh sách sự kiện công khai. Tên tham số query là camelCase.
/// </summary>
public class EventSearchQuery : IValidatableObject
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Từ khóa trong tên sự kiện.</summary>
    [StringLength(200)]
    public string? Q { get; set; }

    public Guid? CategoryId { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    /// <summary>Sự kiện bắt đầu từ thời điểm này.</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>Sự kiện bắt đầu trước hoặc đúng thời điểm này.</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>Có ít nhất một loại vé đang bán giá ≥ minPrice.</summary>
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal? MinPrice { get; set; }

    /// <summary>Có ít nhất một loại vé đang bán giá ≤ maxPrice.</summary>
    [Range(typeof(decimal), "0", "1000000000")]
    public decimal? MaxPrice { get; set; }

    [Range(1, 100_000)]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = DefaultPageSize;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From > To)
        {
            yield return new ValidationResult(
                "to phải sau hoặc bằng from.",
                [nameof(To)]);
        }

        if (MinPrice > MaxPrice)
        {
            yield return new ValidationResult(
                "maxPrice phải lớn hơn hoặc bằng minPrice.",
                [nameof(MaxPrice)]);
        }
    }
}

public class TicketTypeResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }
    public int MaxQuantityPerOrder { get; set; }
    public DateTimeOffset SaleStartsAt { get; set; }
    public DateTimeOffset SaleEndsAt { get; set; }
}

public class EventResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public string OrganizationName { get; set; } = string.Empty;
    public string VenueName { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public List<TicketTypeResponse> TicketTypes { get; set; } = new();
}

/// <summary>
/// Thông tin cho người quản lý sự kiện và Admin: thêm người tạo
/// và kết quả duyệt gần nhất (lý do từ chối nếu có).
/// </summary>
public class ManagedEventResponse : EventResponse
{
    public Guid CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string? LastReviewNote { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
}

/// <summary>Một trang kết quả; controller trả Items và đặt tổng số vào header.</summary>
public sealed record EventPage(List<EventResponse> Items, int TotalCount);

public interface IEventService
{
    /// <summary>
    /// <c>POST /api/events</c>: tạo (tổ chức cá nhân nếu không chỉ định)
    /// rồi gửi duyệt ngay, báo cho PlatformAdmin.
    /// </summary>
    Task<EventResult<ManagedEventResponse>> CreateAndSubmitAsync(
        Guid currentUserId,
        SaveEventRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Tạo sự kiện nháp trong tổ chức. Chỉ Owner/Manager.</summary>
    Task<EventResult<ManagedEventResponse>> CreateDraftAsync(
        Guid organizationId,
        Guid currentUserId,
        EventDetailsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sửa sự kiện Draft/Rejected. Không đổi trạng thái.</summary>
    Task<EventResult<ManagedEventResponse>> UpdateAsync(
        Guid eventId,
        Guid currentUserId,
        EventDetailsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Draft/Rejected → PendingApproval, báo cho PlatformAdmin.</summary>
    Task<EventResult<ManagedEventResponse>> SubmitAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Approved → Published: công khai và mở bán vé.</summary>
    Task<EventResult<ManagedEventResponse>> PublishAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Draft/Approved/Published → Cancelled khi chưa có vé giữ chỗ/bán.</summary>
    Task<EventResult<ManagedEventResponse>> CancelAsync(
        Guid eventId,
        Guid currentUserId,
        CancelEventRequest request,
        CancellationToken cancellationToken = default);
}

public interface IEventQueryService
{
    /// <summary>Sự kiện công khai còn diễn ra, có lọc và phân trang.</summary>
    Task<EventPage> SearchPublishedAsync(
        EventSearchQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Chi tiết theo slug hoặc id. Người ngoài chỉ thấy sự kiện Published;
    /// Owner/Manager của tổ chức thấy mọi trạng thái.
    /// </summary>
    Task<EventResponse?> GetDetailAsync(
        string? slug,
        Guid? eventId,
        Guid? currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Sự kiện của một tổ chức. Chỉ Owner/Manager.</summary>
    Task<EventResult<List<ManagedEventResponse>>> GetByOrganizationAsync(
        Guid organizationId,
        Guid currentUserId,
        EventStatus? status,
        CancellationToken cancellationToken = default);

    /// <summary>Sự kiện thuộc các tổ chức mà người gọi là Owner/Manager.</summary>
    Task<List<ManagedEventResponse>> GetManagedAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}

public interface IEventReviewService
{
    Task<List<ManagedEventResponse>> GetForReviewAsync(
        EventStatus status,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>PendingApproval → Approved. Tổ chức tự xuất bản sau đó.</summary>
    Task<EventResult<ManagedEventResponse>> ApproveAsync(
        Guid eventId,
        Guid adminUserId,
        ReviewEventRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Từ chối: bắt buộc có lý do để người tạo sửa lại.</summary>
    Task<EventResult<ManagedEventResponse>> RejectAsync(
        Guid eventId,
        Guid adminUserId,
        ReviewEventRequest request,
        CancellationToken cancellationToken = default);
}

public class EventPendingCountResponse
{
    public int Count { get; set; }
}

public sealed record EventSubmittedNotification(
    Guid EventId,
    string Title,
    string OrganizationName,
    string SubmittedByName,
    string SubmittedByEmail,
    DateTimeOffset StartsAt,
    bool IsResubmission);

/// <summary>
/// Báo cho PlatformAdmin khi có sự kiện cần duyệt.
/// Lỗi gửi thông báo không được làm hỏng thao tác tạo/sửa sự kiện.
/// </summary>
public interface IEventApprovalNotifier
{
    Task NotifyAdminsAsync(
        EventSubmittedNotification notification,
        CancellationToken cancellationToken = default);
}
