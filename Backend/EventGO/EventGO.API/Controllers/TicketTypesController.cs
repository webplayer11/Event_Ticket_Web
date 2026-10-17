using EventGO.API.Extensions;
using EventGO.Application.TicketTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
public sealed class TicketTypesController(ITicketTypeService service) : ControllerBase
{
    [Authorize, HttpPost("api/events/{eventId:guid}/ticket-types")]
    [ProducesResponseType(typeof(ManagedTicketTypeResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid eventId, SaveTicketTypeRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } uid) return Unauthorized();
        var result = await service.CreateAsync(eventId, uid, request, ct);
        return result.Error == TicketTypeError.None ? StatusCode(201, result.Data) : Error(result.Error);
    }

    [AllowAnonymous, HttpGet("api/events/{eventId:guid}/ticket-types")]
    [ProducesResponseType(typeof(List<ManagedTicketTypeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid eventId, CancellationToken ct)
    {
        var result = await service.GetAsync(eventId, User.GetUserId(), ct);
        return result.Error == TicketTypeError.None ? Ok(result.Data) : Error(result.Error);
    }

    [Authorize, HttpPut("api/ticket-types/{ticketTypeId:guid}")]
    [ProducesResponseType(typeof(ManagedTicketTypeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(Guid ticketTypeId, SaveTicketTypeRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } uid) return Unauthorized();
        var result = await service.UpdateAsync(ticketTypeId, uid, request, ct);
        return result.Error == TicketTypeError.None ? Ok(result.Data) : Error(result.Error);
    }

    [Authorize, HttpPatch("api/ticket-types/{ticketTypeId:guid}/status")]
    [ProducesResponseType(typeof(ManagedTicketTypeResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetStatus(Guid ticketTypeId, UpdateTicketTypeStatusRequest request, CancellationToken ct)
    {
        if (User.GetUserId() is not { } uid) return Unauthorized();
        var result = await service.SetStatusAsync(ticketTypeId, uid, request.IsActive!.Value, ct);
        return result.Error == TicketTypeError.None ? Ok(result.Data) : Error(result.Error);
    }

    private IActionResult Error(TicketTypeError error)
    {
        var (status, code) = error switch
        {
            TicketTypeError.EventNotFound => (404, "EVENT_NOT_FOUND"),
            TicketTypeError.NotFound => (404, "TICKET_TYPE_NOT_FOUND"),
            TicketTypeError.Forbidden => (403, "FORBIDDEN"),
            TicketTypeError.Invalid => (422, "INVALID_TICKET_TYPE"),
            TicketTypeError.CurrencyMismatch => (422, "CURRENCY_MISMATCH"),
            TicketTypeError.SaleWindowClosed => (422, "SALE_WINDOW_CLOSED"),
            TicketTypeError.PriceLocked => (409, "PRICE_LOCKED"),
            TicketTypeError.InventoryCommitted => (409, "INVENTORY_COMMITTED"),
            TicketTypeError.EventNotEditable => (409, "EVENT_NOT_EDITABLE"),
            _ => (409, "TICKET_TYPE_CONFLICT")
        };
        return StatusCode(status, new { code, message = "Không thể xử lý hạng vé theo điều kiện hiện tại." });
    }
}
