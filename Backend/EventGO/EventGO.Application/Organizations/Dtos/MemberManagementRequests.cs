using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Organizations.Dtos;

public class UpdateOrganizationMemberRoleRequest
{
    // 2 = Manager, 3 = Staff. Không đổi được thành Owner,
    // muốn đổi Owner phải dùng chức năng chuyển quyền.
    [Range(2, 3, ErrorMessage = "Role chỉ được là 2 (Manager) hoặc 3 (Staff).")]
    public int Role { get; set; }
}

public class UpdateOrganizationMemberStatusRequest
{
    public bool IsActive { get; set; }
}

public class TransferOrganizationOwnershipRequest
{
    [Required]
    public Guid NewOwnerMemberId { get; set; }
}
