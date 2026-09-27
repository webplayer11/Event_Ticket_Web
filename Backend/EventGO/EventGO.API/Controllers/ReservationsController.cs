using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EventGO.Application.Reservations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
[Authorize]
[Route("api/reservations")]
public sealed class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        CreateReservationRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _reservationService.CreateAsync(
            userId, idempotencyKey, request, cancellationToken);

        return result.Error == ReservationError.None
            ? StatusCode(StatusCodes.Status201Created, result.Reservation)
            : Error(result.Error);
    }

    [HttpGet("{reservationId:guid}")]
    public async Task<IActionResult> Get(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _reservationService.GetAsync(
            reservationId, userId, cancellationToken);

        return result.Error == ReservationError.None
            ? Ok(result.Reservation)
            : Error(result.Error);
    }

    [HttpDelete("{reservationId:guid}")]
    public async Task<IActionResult> Cancel(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            return Unauthorized();
        }

        var result = await _reservationService.CancelAsync(
            reservationId, userId, cancellationToken);

        return result.Error == ReservationError.None
            ? Ok(result.Reservation)
            : Error(result.Error);
    }

    private bool TryGetUserId(out Guid userId) => Guid.TryParse(
        User.FindFirstValue(JwtRegisteredClaimNames.Sub), out userId);

    private IActionResult Error(ReservationError error) => error switch
    {
        ReservationError.EventNotFound or ReservationError.TicketTypeNotFound
            or ReservationError.NotFound => NotFound(ApiError("RESOURCE_NOT_FOUND")),
        ReservationError.InvalidUser => Unauthorized(ApiError("UNAUTHENTICATED")),
        ReservationError.InventoryInsufficient =>
            Conflict(ApiError("INVENTORY_INSUFFICIENT")),
        ReservationError.IdempotencyConflict =>
            Conflict(ApiError("IDEMPOTENCY_KEY_REUSED")),
        ReservationError.InvalidIdempotencyKey =>
            BadRequest(ApiError("INVALID_IDEMPOTENCY_KEY")),
        ReservationError.ConcurrencyConflict =>
            Conflict(ApiError("INVENTORY_CONFLICT")),
        ReservationError.ConfigurationMissing =>
            StatusCode(StatusCodes.Status503ServiceUnavailable,
                ApiError("RESERVATION_CONFIGURATION_MISSING")),
        ReservationError.EventNotSaleable =>
            UnprocessableEntity(ApiError("EVENT_NOT_SALEABLE")),
        ReservationError.TicketTypeNotForEvent
            or ReservationError.TicketTypeNotOnSale
            or ReservationError.QuantityLimitExceeded
            or ReservationError.InvalidRequest =>
            UnprocessableEntity(ApiError(error.ToString().ToUpperInvariant())),
        _ => StatusCode(StatusCodes.Status500InternalServerError,
            ApiError("INTERNAL_ERROR"))
    };

    private static object ApiError(string code) => new
    {
        error = new { code },
    };
}
