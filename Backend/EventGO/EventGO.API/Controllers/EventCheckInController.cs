using EventGO.API.Extensions;
using EventGO.Application.Tickets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

/// <summary>
/// Soát vé tại cổng. Quyền được kiểm tra trong service:
/// người gọi phải có phân công soát vé còn hiệu lực cho sự kiện.
/// </summary>
[ApiController]
[Authorize]
[Route("api/events/{eventId:guid}/check-in")]
public sealed class EventCheckInController : ControllerBase
{
    private readonly ITicketService _ticketService;

    public EventCheckInController(ITicketService ticketService)
    {
        _ticketService = ticketService;
    }

    [HttpPost("validate")]
    public async Task<IActionResult> Validate(
        Guid eventId,
        [FromBody] ValidateTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _ticketService.ValidateQrTokenAsync(
            userId,
            eventId,
            request.QrToken,
            cancellationToken);

        return ToResponse(result);
    }

    [HttpPost]
    public async Task<IActionResult> CheckIn(
        Guid eventId,
        [FromBody] CheckInTicketRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _ticketService.CheckInAsync(
            userId,
            eventId,
            request,
            cancellationToken);

        return ToResponse(result);
    }

    private IActionResult ToResponse(TicketCheckInResult result) => result.Error switch
    {
        TicketCheckInError.None => Ok(result.Ticket),
        TicketCheckInError.Forbidden => StatusCode(
            StatusCodes.Status403Forbidden,
            ApiError("CHECK_IN_NOT_ASSIGNED")),
        TicketCheckInError.TicketNotFound => NotFound(ApiError("TICKET_NOT_FOUND")),
        TicketCheckInError.AlreadyCheckedIn => Conflict(new
        {
            error = new { code = "TICKET_ALREADY_CHECKED_IN" },
            ticket = result.Ticket
        }),
        TicketCheckInError.TicketNotValid => UnprocessableEntity(new
        {
            error = new { code = "TICKET_NOT_VALID" },
            ticket = result.Ticket
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, ApiError("INTERNAL_ERROR"))
    };

    private static object ApiError(string code) => new
    {
        error = new { code },
    };
}
