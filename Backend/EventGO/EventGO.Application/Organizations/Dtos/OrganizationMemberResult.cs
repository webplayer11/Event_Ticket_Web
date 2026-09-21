namespace EventGO.Application.Organizations;

public enum OrganizationMemberError
{
    None,
    NotFound,
    Forbidden,
    UserNotFound,
    AlreadyMember,
    InvalidRole
}

public record OrganizationMemberResult<T>(
    T? Data,
    OrganizationMemberError Error)
    where T : class;