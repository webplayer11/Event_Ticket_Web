using EventGO.Domain.Enums;

namespace EventGO.Application.Authorization;

/// <summary>
/// Bảng quyền theo vai trò trong tổ chức.
/// Mọi service kiểm tra quyền tổ chức đều dùng chung các quy tắc này.
/// </summary>
public static class OrganizationPermissions
{
    // Sửa thông tin tổ chức, xem danh sách thành viên.
    public static bool CanManageOrganization(OrganizationMemberRole role) =>
        role is OrganizationMemberRole.Owner or OrganizationMemberRole.Manager;

    // Thêm/khóa thành viên, đổi vai trò, chuyển quyền Owner.
    public static bool CanManageMembers(OrganizationMemberRole role) =>
        role is OrganizationMemberRole.Owner;

    // Tạo/sửa sự kiện, loại vé, xem doanh thu, phân công Staff.
    public static bool CanManageEvents(OrganizationMemberRole role) =>
        role is OrganizationMemberRole.Owner or OrganizationMemberRole.Manager;
}
