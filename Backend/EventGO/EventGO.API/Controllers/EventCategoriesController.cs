using EventGO.API.Contracts;
using EventGO.Application.Authentication;
using EventGO.Application.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventGO.API.Controllers;

[ApiController]
[ProducesErrorResponseType(typeof(void))]
[Route("api/event-categories")]
public sealed class EventCategoriesController : ControllerBase
{
    private readonly IEventCategoryService _categoryService;

    public EventCategoriesController(IEventCategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    /// <summary>
    /// Danh sách category đang hoạt động (công khai).
    /// <c>includeInactive=true</c> chỉ dành cho PlatformAdmin, để bật lại category đã tắt.
    /// </summary>
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType(typeof(List<EventCategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromQuery] bool includeInactive,
        CancellationToken cancellationToken)
    {
        if (includeInactive && !User.IsInRole(SystemRoles.PlatformAdmin))
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new MessageResponse("Chỉ PlatformAdmin được xem category đã vô hiệu hóa."));
        }

        return Ok(await _categoryService.GetAsync(includeInactive, cancellationToken));
    }

    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    [HttpPost]
    [ProducesResponseType(typeof(EventCategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest, "application/json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateEventCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.CreateAsync(request, cancellationToken);
        return result.Error == EventCategoryError.None
            ? StatusCode(StatusCodes.Status201Created, result.Data)
            : Error(result);
    }

    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(EventCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateEventCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.UpdateAsync(id, request, cancellationToken);
        return result.Error == EventCategoryError.None
            ? Ok(result.Data)
            : Error(result);
    }

    /// <summary>
    /// Bật/tắt category. Không tắt được khi còn sự kiện đã công khai
    /// chưa kết thúc thuộc category (409).
    /// </summary>
    [Authorize(Policy = AuthorizationPolicies.PlatformAdmin)]
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(EventCategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateEventCategoryStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _categoryService.SetStatusAsync(
            id,
            request.IsActive!.Value,
            cancellationToken);

        return result.Error == EventCategoryError.None
            ? Ok(result.Data)
            : Error(result);
    }

    private IActionResult Error(EventCategoryResult result) => result.Error switch
    {
        EventCategoryError.NotFound => NotFound(
            new MessageResponse("Không tìm thấy category.")),
        EventCategoryError.DuplicateSlug => Conflict(
            new MessageResponse("Slug đã được category khác sử dụng.")),
        EventCategoryError.InvalidSlug => BadRequest(
            new MessageResponse(
                "Không tạo được slug từ tên category. Vui lòng nhập slug.",
                [EventCategoryRules.SlugPatternMessage])),
        EventCategoryError.InUse => Conflict(
            new MessageResponse(
                "Không thể vô hiệu hóa category đang có sự kiện mở bán.",
                [$"Còn {result.ActiveEventCount} sự kiện đã công khai và chưa kết thúc thuộc category này."])),
        _ => StatusCode(
            StatusCodes.Status500InternalServerError,
            new MessageResponse("Không thể xử lý yêu cầu."))
    };
}
