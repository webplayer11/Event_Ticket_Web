using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Events;

public static class EventCategoryRules
{
    // Chữ thường không dấu, số, nối bằng một dấu gạch ngang: "hoi-thao-workshop".
    public const string SlugPattern = "^[a-z0-9]+(?:-[a-z0-9]+)*$";

    public const string SlugPatternMessage =
        "Slug chỉ gồm chữ thường không dấu, chữ số và dấu gạch ngang (vd: hoi-thao-workshop).";
}

public enum EventCategoryError
{
    None,
    NotFound,
    // Slug (sau khi chuẩn hóa) đã được category khác dùng.
    DuplicateSlug,
    // Không tạo được slug từ tên (tên toàn ký tự đặc biệt) khi không nhập slug.
    InvalidSlug,
    // Category đang có sự kiện công khai chưa kết thúc: không được vô hiệu hóa.
    InUse
}

public sealed record EventCategoryResult(
    EventCategoryResponse? Data,
    EventCategoryError Error,
    int ActiveEventCount = 0);

public class CreateEventCategoryRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Bỏ trống: tự sinh từ tên ("Âm nhạc" → "am-nhac").</summary>
    [StringLength(150)]
    [RegularExpression(EventCategoryRules.SlugPattern, ErrorMessage = EventCategoryRules.SlugPatternMessage)]
    public string? Slug { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }
}

public class UpdateEventCategoryRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    [RegularExpression(EventCategoryRules.SlugPattern, ErrorMessage = EventCategoryRules.SlugPatternMessage)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}

public class UpdateEventCategoryStatusRequest
{
    // Bắt buộc gửi rõ true/false, không mặc định.
    [Required]
    public bool? IsActive { get; set; }
}

public class EventCategoryResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public interface IEventCategoryService
{
    /// <summary>
    /// Mặc định chỉ category đang hoạt động; includeInactive dành cho Admin.
    /// </summary>
    Task<List<EventCategoryResponse>> GetAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default);

    Task<EventCategoryResult> CreateAsync(
        CreateEventCategoryRequest request,
        CancellationToken cancellationToken = default);

    Task<EventCategoryResult> UpdateAsync(
        Guid categoryId,
        UpdateEventCategoryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bật/tắt category. Chính sách vô hiệu hóa:
    /// bị chặn nếu còn sự kiện Published chưa kết thúc thuộc category;
    /// sự kiện chưa công khai không bị ảnh hưởng, nhưng không thể tạo/sửa
    /// sự kiện để chọn category đã tắt.
    /// </summary>
    Task<EventCategoryResult> SetStatusAsync(
        Guid categoryId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
