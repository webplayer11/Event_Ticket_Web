using System.Text.Json.Serialization;

namespace EventGO.API.Contracts;

/// <summary>
/// Body lỗi nghiệp vụ đang dùng ở các API quản trị/tài khoản/tổ chức:
/// <c>{ "message": "..." }</c> hoặc <c>{ "message": "...", "errors": ["..."] }</c>
/// (xem AuthController, OrganizationsController, PlatformAdminUsersController).
/// Kiểu này chỉ đặt tên cho đúng hình dạng JSON đó để Swagger mô tả được;
/// không phải chuẩn response mới. Lỗi validation DataAnnotations vẫn là
/// ValidationProblemDetails do [ApiController] tự trả.
/// </summary>
public sealed class MessageResponse
{
    public MessageResponse(string message, IReadOnlyList<string>? errors = null)
    {
        Message = message;
        Errors = errors;
    }

    public string Message { get; }

    // Bỏ khỏi JSON khi null, giống các response hiện chỉ có "message".
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<string>? Errors { get; }
}
