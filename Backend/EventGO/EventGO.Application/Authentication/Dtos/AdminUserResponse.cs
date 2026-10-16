namespace EventGO.Application.Authentication.Dtos;

/// <summary>
/// Thông tin tài khoản cho PlatformAdmin: thêm trạng thái hoạt động
/// so với UserResponse, để Admin biết trước khi cấp vai trò hay khóa.
/// </summary>
public class AdminUserResponse : UserResponse
{
    public bool IsActive { get; set; }
}
