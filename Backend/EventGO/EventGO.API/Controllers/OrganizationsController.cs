using EventGO.API.Extensions;
using EventGO.Application.Authentication;
using EventGO.Application.Organizations;
using EventGO.Application.Organizations.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations")]
public class OrganizationsController : ControllerBase
{
    private readonly IOrganizationService _organizationService;

    public OrganizationsController(
        IOrganizationService organizationService)
    {
        _organizationService = organizationService;
    }

    // Chỉ tài khoản có vai trò hệ thống Organizer (do PlatformAdmin cấp)
    // mới được tạo tổ chức. Các thao tác khác trong tổ chức vẫn dựa trên
    // vai trò Owner/Manager/Staff, không cần Organizer.
    [Authorize(Policy = AuthorizationPolicies.Organizer)]
    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (request.Name.Trim().Length < 2)
        {
            ModelState.AddModelError(
                nameof(request.Name),
                "Tên tổ chức phải có ít nhất 2 ký tự sau khi bỏ khoảng trắng.");

            return ValidationProblem(ModelState);
        }

        var response = await _organizationService.CreateAsync(
            userId,
            request,
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine(
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var response = await _organizationService.GetMineAsync(
            userId,
            cancellationToken);

        return Ok(response);
    }

    [HttpGet("{organizationId:guid}")]
    public async Task<IActionResult> GetById(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var organization = await _organizationService.GetByIdAsync(
            organizationId,
            userId,
            cancellationToken);

        if (organization is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tổ chức có thể truy cập."
            });
        }

        return Ok(organization);
    }

    [HttpPut("{organizationId:guid}")]
    public async Task<IActionResult> Update(
        Guid organizationId,
        [FromBody] UpdateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (request.Name.Trim().Length < 2)
        {
            ModelState.AddModelError(
                nameof(request.Name),
                "Tên tổ chức phải có ít nhất 2 ký tự sau khi bỏ khoảng trắng.");

            return ValidationProblem(ModelState);
        }

        var result = await _organizationService.UpdateAsync(
            organizationId,
            userId,
            request,
            cancellationToken);

        if (result.Forbidden)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Chỉ Owner hoặc Manager được cập nhật tổ chức."
            });
        }

        if (result.Organization is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy tổ chức có thể truy cập."
            });
        }

        return Ok(result.Organization);
    }

    [HttpPost("{organizationId:guid}/members")]
    public async Task<IActionResult> AddMember(
        Guid organizationId,
        [FromBody] AddOrganizationMemberRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _organizationService.AddMemberAsync(
            organizationId,
            userId,
            request,
            cancellationToken);

        if (result.Error != OrganizationMemberError.None)
        {
            return MemberErrorResponse(result.Error);
        }

        return StatusCode(StatusCodes.Status201Created, result.Data);
    }

    [HttpGet("{organizationId:guid}/members")]
    public async Task<IActionResult> GetMembers(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _organizationService.GetMembersAsync(
            organizationId,
            userId,
            cancellationToken);

        if (result.Error != OrganizationMemberError.None)
        {
            return MemberErrorResponse(result.Error);
        }

        return Ok(result.Data);
    }

    [HttpPut("{organizationId:guid}/members/{memberId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(
        Guid organizationId,
        Guid memberId,
        [FromBody] UpdateOrganizationMemberRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _organizationService.UpdateMemberRoleAsync(
            organizationId,
            userId,
            memberId,
            request,
            cancellationToken);

        return result.Error == OrganizationMemberError.None
            ? Ok(result.Data)
            : MemberErrorResponse(result.Error);
    }

    [HttpPut("{organizationId:guid}/members/{memberId:guid}/status")]
    public async Task<IActionResult> UpdateMemberStatus(
        Guid organizationId,
        Guid memberId,
        [FromBody] UpdateOrganizationMemberStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _organizationService.UpdateMemberStatusAsync(
            organizationId,
            userId,
            memberId,
            request,
            cancellationToken);

        return result.Error == OrganizationMemberError.None
            ? Ok(result.Data)
            : MemberErrorResponse(result.Error);
    }

    [HttpPost("{organizationId:guid}/transfer-ownership")]
    public async Task<IActionResult> TransferOwnership(
        Guid organizationId,
        [FromBody] TransferOrganizationOwnershipRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _organizationService.TransferOwnershipAsync(
            organizationId,
            userId,
            request,
            cancellationToken);

        return result.Error == OrganizationMemberError.None
            ? Ok(result.Data)
            : MemberErrorResponse(result.Error);
    }

    private IActionResult MemberErrorResponse(OrganizationMemberError error)
    {
        return error switch
        {
            OrganizationMemberError.NotFound => NotFound(new
            {
                message = "Không tìm thấy tổ chức có thể truy cập."
            }),

            OrganizationMemberError.Forbidden =>
                StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Bạn không có quyền thực hiện thao tác này."
                }),

            OrganizationMemberError.UserNotFound => NotFound(new
            {
                message = "Không tìm thấy tài khoản đang hoạt động với email này."
            }),

            OrganizationMemberError.MemberNotFound => NotFound(new
            {
                message = "Không tìm thấy thành viên trong tổ chức."
            }),

            OrganizationMemberError.AlreadyMember => Conflict(new
            {
                message = "Tài khoản đã có trong danh sách thành viên của tổ chức."
            }),

            OrganizationMemberError.InvalidRole => BadRequest(new
            {
                message = "Chỉ được dùng quyền Manager hoặc Staff."
            }),

            OrganizationMemberError.CannotModifyOwner => Conflict(new
            {
                message =
                    "Không thể đổi vai trò hoặc khóa Owner. " +
                    "Hãy chuyển quyền Owner trước."
            }),

            OrganizationMemberError.InvalidTransferTarget => BadRequest(new
            {
                message =
                    "Chỉ chuyển quyền Owner cho thành viên khác đang hoạt động."
            }),

            OrganizationMemberError.Conflict => Conflict(new
            {
                message = "Dữ liệu tổ chức vừa thay đổi. Vui lòng thử lại."
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể xử lý yêu cầu."
            })
        };
    }
}
