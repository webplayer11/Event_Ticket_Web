using EventGO.Application.Authentication.Dtos;

namespace EventGO.Application.Authentication;

public enum AccountAdministrationError
{
    None,
    InvalidRole,
    SelfModificationNotAllowed,
    UserNotFound,
    Conflict
}

public sealed record AccountAdministrationResult(
    AccountAdministrationError Error,
    UserResponse? User = null);

public interface IAccountAdministrationService
{
    Task<AccountAdministrationResult> AssignSystemRoleAsync(
        Guid actorUserId,
        Guid targetUserId,
        string role,
        CancellationToken cancellationToken = default);

    Task<AccountAdministrationResult> UpdateAccountStatusAsync(
        Guid actorUserId,
        Guid targetUserId,
        bool isActive,
        CancellationToken cancellationToken = default);
}
