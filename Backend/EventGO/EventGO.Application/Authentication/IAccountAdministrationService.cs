using EventGO.Application.Authentication.Dtos;

namespace EventGO.Application.Authentication;

public enum AccountAdministrationError
{
    None,
    InvalidRole,
    SelfModificationNotAllowed,
    UserNotFound,
    Conflict,
    // Người thực hiện không còn là PlatformAdmin đang hoạt động
    // (ví dụ vừa bị Admin khác hạ quyền trong cùng thời điểm).
    ActorNotAuthorized,
    // Thao tác sẽ làm hệ thống không còn PlatformAdmin nào.
    LastPlatformAdmin,
    // Tài khoản đang là Owner của tổ chức, phải chuyển quyền trước khi khóa.
    OwnsActiveOrganizations
}

public sealed record AccountAdministrationResult(
    AccountAdministrationError Error,
    UserResponse? User = null);

public interface IAccountAdministrationService
{
    /// <summary>
    /// Tra cứu tài khoản theo email (không phân biệt hoa thường),
    /// để Admin lấy userId khi cấp vai trò hoặc khóa tài khoản.
    /// </summary>
    Task<AdminUserResponse?> FindUserByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<AccountAdministrationResult> AssignSystemRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        string role,
        CancellationToken cancellationToken = default);

    Task<AccountAdministrationResult> UpdateAccountStatusAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
