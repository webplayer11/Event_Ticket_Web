namespace EventGO.Application.Organizations;

public enum OrganizationMemberError
{
    None,
    NotFound,
    Forbidden,
    UserNotFound,
    AlreadyMember,
    InvalidRole,
    MemberNotFound,
    CannotModifyOwner,
    InvalidTransferTarget,
    Conflict
}

public record OrganizationMemberResult<T>(
    T? Data,
    OrganizationMemberError Error)
    where T : class;