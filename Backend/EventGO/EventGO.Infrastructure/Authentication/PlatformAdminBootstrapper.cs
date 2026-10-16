using EventGO.Application.Authentication;
using EventGO.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventGO.Infrastructure.Authentication;

/// <summary>
/// Cấp quyền PlatformAdmin đầu tiên khi khởi động.
/// Chỉ chạy khi cấu hình Bootstrap:PlatformAdminEmail và hệ thống
/// chưa có PlatformAdmin nào. Tài khoản phải đăng ký sẵn qua API;
/// bootstrapper không tạo tài khoản hay mật khẩu.
/// Đặt bằng biến môi trường: Bootstrap__PlatformAdminEmail=admin@example.com
/// </summary>
public sealed class PlatformAdminBootstrapper : IHostedService
{
    public const string ConfigurationKey = "Bootstrap:PlatformAdminEmail";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PlatformAdminBootstrapper> _logger;

    public PlatformAdminBootstrapper(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PlatformAdminBootstrapper> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = _configuration[ConfigurationKey]?.Trim();
        if (string.IsNullOrEmpty(email))
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var existingAdmins = await userManager.GetUsersInRoleAsync(
            SystemRoles.PlatformAdmin);
        if (existingAdmins.Any(x => x.IsActive))
        {
            return;
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            _logger.LogWarning(
                "Không cấp được PlatformAdmin đầu tiên: chưa có tài khoản " +
                "đang hoạt động với email trong {Key}.",
                ConfigurationKey);
            return;
        }

        var result = await userManager.AddToRoleAsync(
            user,
            SystemRoles.PlatformAdmin);
        if (!result.Succeeded)
        {
            _logger.LogError(
                "Không cấp được PlatformAdmin đầu tiên: {Errors}",
                string.Join("; ", result.Errors.Select(x => x.Description)));
            return;
        }

        await userManager.UpdateSecurityStampAsync(user);
        _logger.LogInformation(
            "Đã cấp PlatformAdmin đầu tiên cho tài khoản {UserId}.",
            user.Id);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
