using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EventGO.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var userId))
        {
            return Unauthorized();
        }

        var result = await _orderService.CreateAsync(
            userId, request, cancellationToken);

        if (result.Error == OrderError.None)
        {
            return StatusCode(StatusCodes.Status201Created, result.Order);
        }

        var code = result.Error.ToString().ToUpperInvariant();
        return result.Error switch
        {
            OrderError.ReservationNotFound => NotFound(new { error = new { code } }),
            OrderError.ReservationExpired => Conflict(new { error = new { code = "RESERVATION_EXPIRED" } }),
            OrderError.ReservationNotActive or OrderError.AlreadyCreated
                or OrderError.ConcurrencyConflict => Conflict(new { error = new { code } }),
            OrderError.CurrencyMismatch => UnprocessableEntity(new { error = new { code } }),
            _ => StatusCode(StatusCodes.Status500InternalServerError,
                new { error = new { code = "INTERNAL_ERROR" } })
        };
    }
}
