using EventGO.API.Contracts;
using EventGO.API.Extensions;
using EventGO.Application.Authentication;
using EventGO.Application.Events;
using EventGO.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

/// <summary>Duyệt sự kiện. Chỉ PlatformAdmin.</summary>
[ApiController]
[ProducesErrorResponseType(typeof(void))]
[Route("api/platform-admin/events")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PlatformAdminEventsController : ControllerBase
{
    private readonly IEventReviewService _reviewService;

    public PlatformAdminEventsController(IEventReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Danh sách sự kiện theo trạng thái (tên enum, không phân biệt hoa thường).
    /// Mặc định: PendingApproval (chờ duyệt).
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<ManagedEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    public async Task<IActionResult> Get(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var filter = EventStatus.PendingApproval;
        if (!string.IsNullOrWhiteSpace(status)
            && (!Enum.TryParse(status, ignoreCase: true, out filter)
                || !Enum.IsDefined(filter)
                || int.TryParse(status, out _)))
        {
            return BadRequest(new MessageResponse(
                "Trạng thái sự kiện không hợp lệ.",
                [$"Giá trị hợp lệ: {string.Join(", ", Enum.GetNames<EventStatus>())}."]));
        }

        return Ok(await _reviewService.GetForReviewAsync(filter, cancellationToken));
    }

    /// <summary>Số sự kiện chờ duyệt, để hiện badge thông báo.</summary>
    [HttpGet("pending-count")]
    [ProducesResponseType(typeof(EventPendingCountResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingCount(CancellationToken cancellationToken) =>
        Ok(new EventPendingCountResponse
        {
            Count = await _reviewService.CountPendingAsync(cancellationToken)
        });

    [HttpPost("{eventId:guid}/approve")]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(
        Guid eventId,
        [FromBody] ReviewEventRequest? request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } adminUserId)
        {
            return Unauthorized();
        }

        var result = await _reviewService.ApproveAsync(
            eventId,
            adminUserId,
            request ?? new ReviewEventRequest(),
            cancellationToken);

        return result.Error == EventError.None
            ? Ok(result.Data)
            : this.EventErrorResponse(result);
    }

    [HttpPost("{eventId:guid}/reject")]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        Guid eventId,
        [FromBody] ReviewEventRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } adminUserId)
        {
            return Unauthorized();
        }

        var result = await _reviewService.RejectAsync(
            eventId,
            adminUserId,
            request,
            cancellationToken);

        return result.Error == EventError.None
            ? Ok(result.Data)
            : this.EventErrorResponse(result);
    }
}
