using System.ComponentModel.DataAnnotations;

namespace EventGO.Application.Events;

public enum EventStaffError
{
    None,
    // Sự kiện không tồn tại hoặc user không thuộc tổ chức sở hữu sự kiện.
    NotFound,
    Forbidden,
    // Thành viên không thuộc tổ chức của sự kiện hoặc đã bị khóa.
    MemberNotFound,
    AssignmentNotFound
}

public sealed record EventStaffResult<T>(T? Data, EventStaffError Error)
    where T : class;

public class AssignEventStaffRequest
{
    [Required]
    public Guid OrganizationMemberId { get; set; }
}

public class EventStaffAssignmentResponse
{
    public Guid Id { get; set; }

    public Guid EventId { get; set; }

    public Guid OrganizationMemberId { get; set; }

    public Guid UserId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset AssignedAt { get; set; }
}

public interface IEventStaffService
{
    Task<EventStaffResult<List<EventStaffAssignmentResponse>>> GetAsync(
        Guid eventId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Owner/Manager phân công một thành viên của tổ chức soát vé cho sự kiện.
    /// Phân công đã bị thu hồi trước đó sẽ được kích hoạt lại.
    /// </summary>
    Task<EventStaffResult<EventStaffAssignmentResponse>> AssignAsync(
        Guid eventId,
        Guid currentUserId,
        AssignEventStaffRequest request,
        CancellationToken cancellationToken = default);

    Task<EventStaffResult<EventStaffAssignmentResponse>> RevokeAsync(
        Guid eventId,
        Guid currentUserId,
        Guid assignmentId,
        CancellationToken cancellationToken = default);
}
