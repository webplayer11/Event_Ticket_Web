using System.ComponentModel.DataAnnotations;
using EventGO.API.Extensions;
using EventGO.Application.Authentication;
using EventGO.Application.Authentication.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
[Route("api/platform-admin/users")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
public sealed class PlatformAdminUsersController : ControllerBase
{
    private readonly IAccountAdministrationService _accountAdministration;

    public PlatformAdminUsersController(
        IAccountAdministrationService accountAdministration)
    {
        _accountAdministration = accountAdministration;
    }

    [HttpGet]
    public async Task<IActionResult> FindByEmail(
        [FromQuery, Required, EmailAddress, StringLength(256)] string email,
        CancellationToken cancellationToken)
    {
        var user = await _accountAdministration.FindUserByEmailAsync(
            email,
            cancellationToken);

        return user is null
            ? NotFound(new { message = "Không tìm thấy tài khoản với email này." })
            : Ok(user);
    }

    [HttpPut("{userId:guid}/system-role")]
    public async Task<IActionResult> AssignSystemRole(
        Guid userId,
        [FromBody] AssignSystemRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _accountAdministration.AssignSystemRoleAsync(
            actorUserId,
            userId,
            request.Role,
            cancellationToken);

        return MapResult(result);
    }

    [HttpPut("{userId:guid}/status")]
    public async Task<IActionResult> UpdateAccountStatus(
        Guid userId,
        [FromBody] UpdateAccountStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetCurrentUserId(out var actorUserId))
        {
            return Unauthorized();
        }

        var result = await _accountAdministration.UpdateAccountStatusAsync(
            actorUserId,
            userId,
            request.IsActive,
            cancellationToken);

        return MapResult(result);
    }

    private IActionResult MapResult(AccountAdministrationResult result) =>
        result.Error switch
        {
            AccountAdministrationError.None => Ok(result.User),
            AccountAdministrationError.UserNotFound => NotFound(),
            AccountAdministrationError.InvalidRole => BadRequest(new
            {
                message = "Vai trò hệ thống không hợp lệ."
            }),
            AccountAdministrationError.SelfModificationNotAllowed =>
                Conflict(new
                {
                    message =
                        "Không thể thay đổi vai trò hoặc trạng thái của chính mình."
                }),
            AccountAdministrationError.ActorNotAuthorized =>
                StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "Bạn không còn quyền quản trị hệ thống."
                }),
            AccountAdministrationError.LastPlatformAdmin => Conflict(new
            {
                message = "Không thể tước quyền Admin cuối cùng của hệ thống."
            }),
            AccountAdministrationError.OwnsActiveOrganizations => Conflict(new
            {
                message =
                    "Tài khoản đang là Owner của tổ chức. " +
                    "Hãy chuyển quyền Owner trước khi khóa."
            }),
            _ => Conflict(new
            {
                message = "Tài khoản đã thay đổi. Vui lòng thử lại."
            })
        };

    private bool TryGetCurrentUserId(out Guid userId)
    {
        userId = User.GetUserId() ?? Guid.Empty;
        return userId != Guid.Empty;
    }
}
