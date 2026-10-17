using EventGO.API.Extensions;
using EventGO.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
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
    public async Task<IActionResult> GetPublished(CancellationToken cancellationToken) =>
        Ok(await _eventService.GetPublishedAsync(cancellationToken));

    [AllowAnonymous]
    [HttpGet("{eventId:guid}")]
    public async Task<IActionResult> GetPublishedById(
        Guid eventId,
        CancellationToken cancellationToken)
    {
        var evt = await _eventService.GetPublishedByIdAsync(eventId, cancellationToken);
        return evt is null
            ? NotFound(new { message = "Không tìm thấy sự kiện." })
            : Ok(evt);
    }

    /// <summary>Sự kiện mình quản lý (Owner/Manager), kèm trạng thái duyệt.</summary>
    [HttpGet("mine")]
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

[ApiController]
[AllowAnonymous]
[Route("api/event-categories")]
public sealed class EventCategoriesController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventCategoriesController(IEventService eventService)
    {
        _eventService = eventService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await _eventService.GetCategoriesAsync(cancellationToken));
}

internal static class EventErrorResponses
{
    public static IActionResult EventErrorResponse<T>(
        this ControllerBase controller,
        EventResult<T> result)
        where T : class =>
        result.Error switch
        {
            EventError.NotFound => controller.NotFound(new
            {
                message = "Không tìm thấy sự kiện hoặc tổ chức có thể truy cập."
            }),
            EventError.Forbidden => controller.StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Bạn không có quyền quản lý sự kiện của tổ chức này."
            }),
            EventError.CategoryNotFound => controller.BadRequest(new
            {
                message = "Thể loại sự kiện không hợp lệ."
            }),
            EventError.Invalid => controller.BadRequest(new
            {
                message = "Dữ liệu sự kiện không hợp lệ.",
                errors = result.Errors
            }),
            EventError.InvalidStatus => controller.Conflict(new
            {
                message = "Trạng thái hiện tại của sự kiện không cho phép thao tác này."
            }),
            EventError.Conflict => controller.Conflict(new
            {
                message = "Sự kiện vừa được cập nhật bởi người khác. Vui lòng tải lại."
            }),
            _ => controller.StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể xử lý yêu cầu."
            })
        };
}
