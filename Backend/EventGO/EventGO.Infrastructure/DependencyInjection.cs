using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EventGO.Application.Authentication;
using EventGO.Infrastructure.Authentication;
using EventGO.Infrastructure.Identity;
using EventGO.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using EventGO.Application.Organizations;
using EventGO.Infrastructure.Organizations;

namespace EventGO.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("DefaultConnection");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Chưa cấu hình ConnectionStrings:DefaultConnection.");
        }

        services.AddDbContext<EventGoDbContext>(options =>
        {
            options.UseSqlServer(connectionString);
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan =
                    TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<EventGoDbContext>()
            .AddSignInManager();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Issuer),
                "Jwt:Issuer không được để trống.")
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Audience không được để trống.")
            .Validate(
                options => options.HasValidSecretKey(),
                "Jwt:SecretKey phải là Base64 của ít nhất 32 byte.")
            .Validate(
                options => options.AccessTokenMinutes is > 0 and <= 60,
                "Jwt:AccessTokenMinutes phải từ 1 đến 60.")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services
            .AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                options.MapInboundClaims = false;
                options.SaveToken = false;

                options.TokenValidationParameters =
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        RequireSignedTokens = true,
                        RequireExpirationTime = true,

                        ValidIssuer = jwt.Issuer,
                        ValidAudience = jwt.Audience,

                        IssuerSigningKey = new SymmetricSecurityKey(
                            Convert.FromBase64String(jwt.SecretKey)),

                        ValidAlgorithms =
                            new[] { SecurityAlgorithms.HmacSha256 },

                        NameClaimType = JwtRegisteredClaimNames.Sub,
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var subject = context.Principal?
                            .FindFirstValue(JwtRegisteredClaimNames.Sub);

                        var stamp = context.Principal?
                            .FindFirstValue("security_stamp");

                        if (!Guid.TryParse(subject, out var userId)
                            || string.IsNullOrWhiteSpace(stamp))
                        {
                            context.Fail("Token không hợp lệ.");
                            return;
                        }

                        var userManager = context.HttpContext
                            .RequestServices
                            .GetRequiredService<
                                UserManager<ApplicationUser>>();

                        var user = await userManager.FindByIdAsync(
                            userId.ToString());

                        if (user is null
                            || !user.IsActive
                            || user.SecurityStamp != stamp
                            || await userManager.IsLockedOutAsync(user))
                        {
                            context.Fail(
                                "Tài khoản hoặc phiên đăng nhập không hợp lệ.");
                        }
                    }
                };
            });

        services.AddScoped<JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IOrganizationService, OrganizationService>();

        return services;
    }
}