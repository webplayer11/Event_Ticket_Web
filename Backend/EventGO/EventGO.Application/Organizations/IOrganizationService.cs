using EventGO.Application.Organizations.Dtos;

namespace EventGO.Application.Organizations;

public interface IOrganizationService
{
    Task<OrganizationResponse> CreateAsync(
        Guid currentUserId,
        CreateOrganizationRequest request,
        CancellationToken cancellationToken = default);

    Task<List<OrganizationResponse>> GetMineAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);
    Task<OrganizationResponse?> GetByIdAsync(
    Guid organizationId,
    Guid currentUserId,
    CancellationToken cancellationToken = default);

    Task<(OrganizationResponse? Organization, bool Forbidden)> UpdateAsync(
        Guid organizationId,
        Guid currentUserId,
        UpdateOrganizationRequest request,
        CancellationToken cancellationToken = default);
    Task<OrganizationMemberResult<OrganizationMemberResponse>> AddMemberAsync(
    Guid organizationId,
    Guid currentUserId,
    AddOrganizationMemberRequest request,
    CancellationToken cancellationToken = default);

    Task<OrganizationMemberResult<List<OrganizationMemberResponse>>> GetMembersAsync(
        Guid organizationId,
        Guid currentUserId,
        CancellationToken cancellationToken = default);
}