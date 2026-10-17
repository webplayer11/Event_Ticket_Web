using EventGO.API.Contracts;
using EventGO.API.Extensions;
using EventGO.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
[ProducesErrorResponseType(typeof(void))]
[Authorize]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    /// <summary>Sự kiện đã được duyệt và còn diễn ra.</summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublished(CancellationToken cancellationToken) =>
        Ok(await _eventService.GetPublishedAsync(cancellationToken));

    [AllowAnonymous]
    [HttpGet("{eventId:guid}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublishedById(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var evt = await _eventService.GetPublishedByIdAsync(eventId, cancellationToken);
        return evt is null
            ? NotFound(new MessageResponse("Không tìm thấy sự kiện."))
            : Ok(evt);
    }

    /// <summary>Sự kiện mình quản lý (Owner/Manager), kèm trạng thái duyệt.</summary>
    [HttpGet("mine")]
    [ProducesResponseType(typeof(List<ManagedEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        return Ok(await _eventService.GetManagedAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Mọi người dùng đã đăng nhập đều tạo được sự kiện. Sự kiện ở trạng thái
    /// chờ duyệt cho tới khi PlatformAdmin duyệt.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _eventService.CreateAsync(userId, request, cancellationToken);
        return result.Error == EventError.None
            ? StatusCode(StatusCodes.Status201Created, result.Data)
            : this.EventErrorResponse(result);
    }

    [HttpPut("{eventId:guid}")]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid eventId,
        [FromBody] SaveEventRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _eventService.UpdateAsync(
            eventId,
            userId,
            request,
            cancellationToken);

        return result.Error == EventError.None
            ? Ok(result.Data)
            : this.EventErrorResponse(result);
    }
}

internal static class EventErrorResponses
{
    public static IActionResult EventErrorResponse<T>(
        this ControllerBase controller,
        EventResult<T> result)
        where T : class =>
        result.Error switch
        {
            EventError.NotFound => controller.NotFound(new MessageResponse(
                "Không tìm thấy sự kiện hoặc tổ chức có thể truy cập.")),
            EventError.Forbidden => controller.StatusCode(
                StatusCodes.Status403Forbidden,
                new MessageResponse("Bạn không có quyền quản lý sự kiện của tổ chức này.")),
            EventError.CategoryNotFound => controller.BadRequest(new MessageResponse(
                "Thể loại sự kiện không hợp lệ hoặc đã bị vô hiệu hóa.")),
            EventError.Invalid => controller.BadRequest(new MessageResponse(
                "Dữ liệu sự kiện không hợp lệ.",
                result.Errors)),
            EventError.InvalidStatus => controller.Conflict(new MessageResponse(
                "Trạng thái hiện tại của sự kiện không cho phép thao tác này.")),
            EventError.Conflict => controller.Conflict(new MessageResponse(
                "Sự kiện vừa được cập nhật bởi người khác. Vui lòng tải lại.")),
            _ => controller.StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse("Không thể xử lý yêu cầu."))
        };
}
