using EventGO.API.Contracts;
using EventGO.API.Extensions;
using EventGO.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

/// <summary>
/// Sự kiện trong phạm vi một tổ chức. Chỉ Owner/Manager còn hiệu lực của
/// tổ chức đang hoạt động; không thuộc tổ chức → 404, Staff → 403.
/// </summary>
[ApiController]
[ProducesErrorResponseType(typeof(void))]
[Authorize]
[Route("api/organizations/{organizationId:guid}/events")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class OrganizationEventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IEventQueryService _queryService;

    public OrganizationEventsController(IEventService eventService, IEventQueryService queryService)
    {
        _eventService = eventService;
        _queryService = queryService;
    }

    /// <summary>Tạo sự kiện nháp (Draft). Gửi duyệt bằng POST /api/events/{id}/submit.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        Guid organizationId,
        [FromBody] EventDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _eventService.CreateDraftAsync(
            organizationId,
            userId,
            request,
            cancellationToken);

        return result.Error == EventError.None
            ? StatusCode(StatusCodes.Status201Created, result.Data)
            : this.EventErrorResponse(result);
    }

    /// <summary>
    /// Mọi sự kiện của tổ chức, mới nhất trước. ?status= lọc theo tên trạng thái.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ManagedEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid organizationId,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        if (!this.TryParseStatus(status, out var filter, out var statusError))
        {
            return statusError!;
        }

        var result = await _queryService.GetByOrganizationAsync(
            organizationId,
            userId,
            filter,
            cancellationToken);

        return result.Error == EventError.None
            ? Ok(result.Data)
            : this.EventErrorResponse(result);
    }
}
