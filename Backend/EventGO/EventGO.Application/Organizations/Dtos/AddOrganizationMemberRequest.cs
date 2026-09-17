using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Organizations.Dtos;

public class AddOrganizationMemberRequest
{
    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    // 2 = Manager, 3 = Staff
    [Range(2, 3, ErrorMessage = "Role chỉ được là 2 (Manager) hoặc 3 (Staff).")]
    public int Role { get; set; } = 3;
}