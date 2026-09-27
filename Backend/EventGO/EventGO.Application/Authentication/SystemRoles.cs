namespace EventGO.Application.Authentication;

public static class SystemRoles
{
    public const string Customer = "Customer";

    public const string PlatformAdmin = "PlatformAdmin";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Customer,
            PlatformAdmin
        };
}

public static class AuthorizationPolicies
{
    public const string PlatformAdmin = "PlatformAdmin";
}
