using System.ComponentModel.DataAnnotations;

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
    Conflict
}

public sealed record EventResult<T>(
    T? Data,
    EventError Error,
    IReadOnlyList<string>? Errors = null)
    where T : class;

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

public class SaveEventRequest
{
    /// <summary>
    /// Tổ chức sở hữu sự kiện. Bỏ trống: dùng tổ chức cá nhân của người tạo
    /// (tự tạo ở lần đầu). Có giá trị: người tạo phải là Owner/Manager.
    /// Chỉ dùng khi tạo mới; khi sửa sự kiện thì bỏ qua.
    /// </summary>
    public Guid? OrganizationId { get; set; }

    // Tên/thông tin ban tổ chức: dùng để đặt tên tổ chức cá nhân
    // khi hệ thống tự tạo. Bỏ qua nếu đã có tổ chức.
    [StringLength(200)]
    public string? OrganizerName { get; set; }

    [StringLength(4000)]
    public string? OrganizerInfo { get; set; }

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

    [Required]
    [MinLength(1, ErrorMessage = "Sự kiện phải có ít nhất một loại vé.")]
    [MaxLength(20)]
    public List<TicketTypeRequest> TicketTypes { get; set; } = new();
}

public class ReviewEventRequest
{
    // Ghi chú khi duyệt (không bắt buộc) hoặc lý do từ chối (bắt buộc).
    [StringLength(2000)]
    public string? Note { get; set; }
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

public interface IEventService
{
    /// <summary>
    /// Tạo sự kiện ở trạng thái chờ duyệt và báo cho PlatformAdmin.
    /// </summary>
    Task<EventResult<ManagedEventResponse>> CreateAsync(
        Guid currentUserId,
        SaveEventRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sửa sự kiện đang chờ duyệt hoặc bị từ chối. Sự kiện bị từ chối
    /// sau khi sửa sẽ quay lại hàng chờ duyệt và Admin được báo lại.
    /// </summary>
    Task<EventResult<ManagedEventResponse>> UpdateAsync(
        Guid eventId,
        Guid currentUserId,
        SaveEventRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>Sự kiện thuộc các tổ chức mà người gọi là Owner/Manager.</summary>
    Task<List<ManagedEventResponse>> GetManagedAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Sự kiện đã công khai (đã được duyệt).</summary>
    Task<List<EventResponse>> GetPublishedAsync(
        CancellationToken cancellationToken = default);

    Task<EventResponse?> GetPublishedByIdAsync(
        Guid eventId,
        CancellationToken cancellationToken = default);
}

public interface IEventReviewService
{
    Task<List<ManagedEventResponse>> GetForReviewAsync(
        Domain.Enums.EventStatus status,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(CancellationToken cancellationToken = default);

    /// <summary>Duyệt: sự kiện được công khai và mở bán vé.</summary>
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
