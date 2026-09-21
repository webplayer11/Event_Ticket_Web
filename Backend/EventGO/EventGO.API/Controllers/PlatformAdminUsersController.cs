using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
            _ => Conflict(new
            {
                message = "Tài khoản đã thay đổi. Vui lòng thử lại."
            })
        };

    private bool TryGetCurrentUserId(out Guid userId)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return Guid.TryParse(subject, out userId);
    }
}
