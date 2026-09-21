using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Trelix.Core.Middleware;
using Trelix.Server.Features.Authentication;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Infrastructure.Middleware;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Infrastructure;

/// <summary>集中装配 Server 的存储、认证、用例服务与 HTTP 基础能力。</summary>
public static class ServerServices
{
    /// <summary>注册持久化、认证授权、防伪造、错误处理及两组 OpenAPI 文档。</summary>
    /// <param name="services">待注册服务的依赖注入集合。</param>
    /// <param name="environment">当前宿主环境及内容根目录。</param>
    /// <returns>原服务集合。</returns>
    public static IServiceCollection AddTrelix(this IServiceCollection services, IHostEnvironment environment)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<StorageSettings>();
        services.AddDbContext<TrelixDbContext>((provider, options) =>
            options.UseSqlite(provider.GetRequiredService<StorageSettings>().ConnectionString));
        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<IPasswordHasher<Administrator>, PasswordHasher<Administrator>>();
        services.AddScoped<AdministratorSessionService>();
        services.AddScoped<ApplicationTokenService>();
        services.AddScoped<AdministratorCookieEvents>();
        services.AddScoped<ManagementAntiforgeryFilter>();
        services.AddScoped<IAuthorizationHandler, ApplicationScopeHandler>();
        services.AddHttpContextAccessor();

        services.AddDataProtection().SetApplicationName("Trelix");
        services.AddOptions<KeyManagementOptions>().Configure<StorageSettings, ILoggerFactory>((options, storage, logs) =>
            options.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(Path.Combine(storage.DataDirectory, "keys")), logs));

        var securePolicy = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        services.AddAntiforgery(options =>
        {
            options.HeaderName = AuthenticationConstants.CsrfHeader;
            options.Cookie.Name = "Trelix.Antiforgery";
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.SecurePolicy = securePolicy;
        });
        services.AddAuthentication(AuthenticationConstants.AdministratorScheme)
            .AddCookie(AuthenticationConstants.AdministratorScheme, options =>
            {
                options.Cookie.Name = "Trelix.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = securePolicy;
                options.ExpireTimeSpan = AuthenticationConstants.SessionLifetime;
                options.SlidingExpiration = false;
                options.EventsType = typeof(AdministratorCookieEvents);
            })
            .AddScheme<AuthenticationSchemeOptions, ApplicationTokenHandler>(AuthenticationConstants.ApplicationScheme, _ => { });
        services.AddOptions<CookieAuthenticationOptions>(AuthenticationConstants.AdministratorScheme)
            .Configure<TimeProvider>((options, time) => options.TimeProvider = time);
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthenticationConstants.AdministratorPolicy, policy => policy
                .AddAuthenticationSchemes(AuthenticationConstants.AdministratorScheme).RequireAuthenticatedUser().RequireClaim(System.Security.Claims.ClaimTypes.NameIdentifier, "1"))
            .AddPolicy(AuthenticationConstants.ApplicationPolicy, policy => policy
                .AddAuthenticationSchemes(AuthenticationConstants.ApplicationScheme).RequireAuthenticatedUser());

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // 保留原固定窗口配置供对照。
            // options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
            //     context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            //     _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            // 每个来源 IP 最多积攒 10 个令牌，每 6 秒补充 1 个；耗尽后立即拒绝。
            options.AddPolicy("login", context => RateLimitPartition.GetTokenBucketLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                static _ => new TokenBucketRateLimiterOptions
                {
                    TokenLimit = 10,
                    TokensPerPeriod = 1,
                    ReplenishmentPeriod = TimeSpan.FromSeconds(6),
                    AutoReplenishment = true,
                    QueueLimit = 0
                }));
        });
        services.AddProblemDetails(options => options.CustomizeProblemDetails = ApiErrors.Customize);
        services.AddExceptionHandler<ApiOperationExceptionHandler>();
        services.AddExceptionHandler<SafeExceptionHandler>();
        services.AddControllers(options => options.Filters.AddService<ManagementAntiforgeryFilter>())
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
                ApiErrors.Result(context.HttpContext, 400, "invalid_request", "请求字段缺失或格式无效。"));
        services.AddOpenApi("admin");
        services.AddOpenApi("application");
        return services;
    }
}
