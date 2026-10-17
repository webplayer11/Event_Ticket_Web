using EventGO.API.Contracts;
using EventGO.API.Extensions;
using EventGO.Application.Events;
using EventGO.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
[ProducesErrorResponseType(typeof(void))]
[Authorize]
[Route("api/events")]
public sealed class EventsController : ControllerBase
{
    public const string TotalCountHeader = "X-Total-Count";

    private readonly IEventService _eventService;
    private readonly IEventQueryService _queryService;

    public EventsController(IEventService eventService, IEventQueryService queryService)
    {
        _eventService = eventService;
        _queryService = queryService;
    }

    /// <summary>
    /// Sự kiện đã công khai và chưa kết thúc, sắp theo thời gian bắt đầu.
    /// Body là mảng sự kiện của trang hiện tại; tổng số kết quả ở header X-Total-Count.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(List<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> Search(
        [FromQuery] EventSearchQuery query,
        CancellationToken cancellationToken)
    {
        var page = await _queryService.SearchPublishedAsync(query, cancellationToken);
        Response.Headers[TotalCountHeader] = page.TotalCount.ToString();
        return Ok(page.Items);
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

        return Ok(await _queryService.GetManagedAsync(userId, cancellationToken));
    }

    /// <summary>
    /// Chi tiết theo slug. Người ngoài chỉ thấy sự kiện Published;
    /// Owner/Manager của tổ chức xem được cả bản nháp.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetBySlug(string slug, CancellationToken cancellationToken) =>
        GetDetailAsync(slug, null, cancellationToken);

    /// <summary>Chi tiết theo id (giữ cho client cũ). Cùng quy tắc với slug.</summary>
    [AllowAnonymous]
    [HttpGet("{eventId:guid}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(Guid eventId, CancellationToken cancellationToken) =>
        GetDetailAsync(null, eventId, cancellationToken);

    /// <summary>
    /// Tạo và gửi duyệt ngay. Bỏ trống organizationId: dùng tổ chức cá nhân
    /// (tự tạo ở lần đầu). Tạo nháp trong tổ chức: POST /api/organizations/{id}/events.
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

        var result = await _eventService.CreateAndSubmitAsync(userId, request, cancellationToken);
        return result.Error == EventError.None
            ? StatusCode(StatusCodes.Status201Created, result.Data)
            : this.EventErrorResponse(result);
    }

    /// <summary>
    /// Sửa sự kiện Draft/Rejected. ticketTypes = null giữ nguyên loại vé,
    /// mảng thì thay toàn bộ. Không đổi trạng thái.
    /// </summary>
    [HttpPut("{eventId:guid}")]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid eventId,
        [FromBody] EventDetailsRequest request,
        CancellationToken cancellationToken)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await _eventService.UpdateAsync(eventId, userId, request, cancellationToken);
        return result.Error == EventError.None
            ? Ok(result.Data)
            : this.EventErrorResponse(result);
    }

    /// <summary>Draft/Rejected → PendingApproval; PlatformAdmin được báo.</summary>
    [HttpPost("{eventId:guid}/submit")]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Submit(Guid eventId, CancellationToken cancellationToken) =>
        RunAsync(userId => _eventService.SubmitAsync(eventId, userId, cancellationToken));

    /// <summary>Approved → Published: công khai và mở bán vé.</summary>
    [HttpPost("{eventId:guid}/publish")]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Publish(Guid eventId, CancellationToken cancellationToken) =>
        RunAsync(userId => _eventService.PublishAsync(eventId, userId, cancellationToken));

    /// <summary>
    /// Draft/Approved/Published → Cancelled. Bị chặn (409) khi đã có vé
    /// được giữ chỗ/bán vì chưa có chính sách hoàn tiền.
    /// </summary>
    [HttpPost("{eventId:guid}/cancel")]
    [ProducesResponseType(typeof(ManagedEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public Task<IActionResult> Cancel(
        Guid eventId,
        [FromBody] CancelEventRequest? request,
        CancellationToken cancellationToken) =>
        RunAsync(userId => _eventService.CancelAsync(
            eventId,
            userId,
            request ?? new CancelEventRequest(),
            cancellationToken));

    private async Task<IActionResult> GetDetailAsync(
        string? slug,
        Guid? eventId,
        CancellationToken cancellationToken)
    {
        var evt = await _queryService.GetDetailAsync(
            slug,
            eventId,
            User.GetUserId(),
            cancellationToken);

        return evt is null
            ? NotFound(new MessageResponse("Không tìm thấy sự kiện."))
            : Ok(evt);
    }

    private async Task<IActionResult> RunAsync(
        Func<Guid, Task<EventResult<ManagedEventResponse>>> action)
    {
        if (User.GetUserId() is not { } userId)
        {
            return Unauthorized();
        }

        var result = await action(userId);
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
            EventError.TicketsSold => controller.Conflict(new MessageResponse(
                "Sự kiện đã có vé được giữ chỗ hoặc bán. Chưa có chính sách hoàn tiền nên không thể hủy.")),
            _ => controller.StatusCode(
                StatusCodes.Status500InternalServerError,
                new MessageResponse("Không thể xử lý yêu cầu."))
        };

    /// <summary>
    /// Đọc tham số ?status= (tên enum, không phân biệt hoa thường; không nhận số).
    /// Trả false kèm body 400 khi giá trị không hợp lệ.
    /// </summary>
    public static bool TryParseStatus(
        this ControllerBase controller,
        string? value,
        out EventStatus? status,
        out IActionResult? error)
    {
        status = null;
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (Enum.TryParse<EventStatus>(value, ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            && !int.TryParse(value, out _))
        {
            status = parsed;
            return true;
        }

        error = controller.BadRequest(new MessageResponse(
            "Trạng thái sự kiện không hợp lệ.",
            [$"Giá trị hợp lệ: {string.Join(", ", Enum.GetNames<EventStatus>())}."]));
        return false;
    }
}
