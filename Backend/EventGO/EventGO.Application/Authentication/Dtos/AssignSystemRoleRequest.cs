using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Authentication.Dtos;

public sealed class AssignSystemRoleRequest
{
    [Required(ErrorMessage = "Vui lòng chọn vai trò hệ thống.")]
    [StringLength(50)]
    public string Role { get; set; } = string.Empty;
}
