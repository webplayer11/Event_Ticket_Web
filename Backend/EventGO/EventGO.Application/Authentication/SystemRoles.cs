namespace EventGO.Application.Authentication;

public static class SystemRoles
{
    public const string Customer = "Customer";

    // Ban tổ chức sự kiện: được tạo tổ chức. Do PlatformAdmin cấp.
    public const string Organizer = "Organizer";

    public const string PlatformAdmin = "PlatformAdmin";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Customer,
            Organizer,
            PlatformAdmin
        };
}

public static class AuthorizationPolicies
{
    public const string PlatformAdmin = "PlatformAdmin";

    public const string Organizer = "Organizer";
}
