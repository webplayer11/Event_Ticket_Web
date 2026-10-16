using EventGO.API.Extensions;
using EventGO.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

/// <summary>
/// Owner/Manager của tổ chức phân công thành viên soát vé cho sự kiện.
/// </summary>
[ApiController]
[Authorize]
[Route("api/events/{eventId:guid}/staff")]
public sealed class EventStaffController : ControllerBase
{
    private readonly IEventStaffService _eventStaffService;

    public EventStaffController(IEventStaffService eventStaffService)
    {
        _eventStaffService = eventStaffService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _eventStaffService.GetAsync(
            eventId,
            userId,
            cancellationToken);

        return result.Error == EventStaffError.None
            ? Ok(result.Data)
            : Error(result.Error);
    }

    [HttpPost]
    public async Task<IActionResult> Assign(
        Guid eventId,
        [FromBody] AssignEventStaffRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _eventStaffService.AssignAsync(
            eventId,
            userId,
            request,
            cancellationToken);

        return result.Error == EventStaffError.None
            ? Ok(result.Data)
            : Error(result.Error);
    }

    [HttpDelete("{assignmentId:guid}")]
    public async Task<IActionResult> Revoke(
        Guid eventId,
        Guid assignmentId,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _eventStaffService.RevokeAsync(
            eventId,
            userId,
            assignmentId,
            cancellationToken);

        return result.Error == EventStaffError.None
            ? Ok(result.Data)
            : Error(result.Error);
    }

    private IActionResult Error(EventStaffError error) => error switch
    {
        EventStaffError.NotFound => NotFound(new
        {
            message = "Không tìm thấy sự kiện có thể truy cập."
        }),
        EventStaffError.Forbidden => StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = "Chỉ Owner hoặc Manager được phân công soát vé."
        }),
        EventStaffError.MemberNotFound => NotFound(new
        {
            message = "Không tìm thấy thành viên đang hoạt động của tổ chức."
        }),
        EventStaffError.AssignmentNotFound => NotFound(new
        {
            message = "Không tìm thấy phân công."
        }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new
        {
            message = "Không thể xử lý yêu cầu."
        })
    };
}
