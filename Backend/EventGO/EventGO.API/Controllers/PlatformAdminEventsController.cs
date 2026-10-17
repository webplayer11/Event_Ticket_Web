using EventGO.API.Extensions;
using EventGO.Application.Authentication;
using EventGO.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

/// <summary>Duyệt sự kiện. Chỉ PlatformAdmin.</summary>
[ApiController]
[Route("api/platform-admin/events")]
[Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
public sealed class PlatformAdminEventsController : ControllerBase
{
    private readonly IEventReviewService _reviewService;

    public PlatformAdminEventsController(IEventReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>
    /// Danh sách sự kiện theo trạng thái. Mặc định: PendingApproval (chờ duyệt).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        Ok(await _reviewService.GetForReviewAsync(status, cancellationToken));

    /// <summary>Số sự kiện chờ duyệt, để hiện badge thông báo.</summary>
    [HttpGet("pending-count")]
    public async Task<IActionResult> GetPendingCount(CancellationToken cancellationToken) =>
        Ok(new { count = await _reviewService.CountPendingAsync(cancellationToken) });

    [HttpPost("{eventId:guid}/approve")]
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
