using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EventGO.API.Extensions;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Id user lấy từ claim "sub" của JWT, hoặc null nếu không hợp lệ.
    /// </summary>
    public static Guid? GetUserId(this ClaimsPrincipal user) =>
        Guid.TryParse(
            user.FindFirstValue(JwtRegisteredClaimNames.Sub),
            out var userId)
            ? userId
            : null;
}
