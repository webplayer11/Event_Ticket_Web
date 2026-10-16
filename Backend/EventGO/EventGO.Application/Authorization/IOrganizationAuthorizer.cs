using EventGO.Domain.Enums;

namespace EventGO.Application.Authorization;

public sealed record OrganizationMembership(
    Guid MemberId,
    Guid OrganizationId,
    OrganizationMemberRole Role);

public interface IOrganizationAuthorizer
{
    /// <summary>
    /// Membership còn hiệu lực của user trong tổ chức đang hoạt động,
    /// hoặc null nếu user không thuộc tổ chức.
    /// </summary>
    Task<OrganizationMembership?> GetMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Membership của user trong tổ chức sở hữu sự kiện.
    /// </summary>
    Task<OrganizationMembership?> GetMembershipForEventAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Id phân công soát vé còn hiệu lực của user cho sự kiện, hoặc null.
    /// Phân công chỉ hợp lệ khi membership và tổ chức đều còn hoạt động.
    /// </summary>
    Task<Guid?> GetActiveStaffAssignmentIdAsync(
        Guid eventId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
