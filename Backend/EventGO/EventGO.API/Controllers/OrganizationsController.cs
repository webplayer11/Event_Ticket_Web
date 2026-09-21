using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateOrganizationRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
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
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
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
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
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
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
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
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
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
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (!Guid.TryParse(subject, out var userId))
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

            OrganizationMemberError.AlreadyMember => Conflict(new
            {
                message = "Tài khoản đã có trong danh sách thành viên của tổ chức."
            }),

            OrganizationMemberError.InvalidRole => BadRequest(new
            {
                message = "Chỉ được thêm thành viên với quyền Manager hoặc Staff."
            }),

            _ => StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể xử lý yêu cầu."
            })
        };
    }
}