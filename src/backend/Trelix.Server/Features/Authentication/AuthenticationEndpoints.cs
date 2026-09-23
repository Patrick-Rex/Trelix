using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Features.Authentication;

/// <summary>映射管理员会话和防伪造 Minimal API。</summary>
public static class AuthenticationEndpoints
{
    /// <summary>注册管理员会话端点，登录与防伪造令牌允许匿名访问。</summary>
    /// <param name="admin">已配置管理员策略的路由组。</param>
    public static void MapAuthentication(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/auth").WithTags("Authentication");
        group.MapGet("/antiforgery", Antiforgery).AllowAnonymous().WithSummary("获取防伪造令牌；登录前及登录后分别获取。");
        group.MapPost("/login", Login).AllowAnonymous().RequireRateLimiting("login")
            .ProducesProblem(429).WithSummary("验证管理员凭证并建立固定有效期的 Cookie 会话。");
        group.MapGet("/session", Session).WithSummary("查询当前管理员会话及到期时间。");
        group.MapPost("/logout", Logout).WithSummary("使当前会话立即失效并清除登录 Cookie。");
    }

    /// <summary>生成绑定当前身份的请求令牌并保存配套 Cookie。</summary>
    /// <param name="context">当前请求。</param>
    /// <param name="antiforgery">防伪造服务。</param>
    /// <returns>令牌和请求头名称。</returns>
    private static Ok<AntiforgeryResponse> Antiforgery(HttpContext context, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        return TypedResults.Ok(new AntiforgeryResponse(tokens.RequestToken!, AuthenticationConstants.CsrfHeader));
    }

    /// <summary>校验凭证并建立管理员会话。</summary>
    /// <param name="request">登录凭证。</param>
    /// <param name="context">当前请求。</param>
    /// <param name="sessions">会话服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>会话信息或凭证错误。</returns>
    private static async Task<Results<Ok<SessionResponse>, ProblemHttpResult>> Login(LoginRequest request,
        HttpContext context, AdministratorSessionService sessions, CancellationToken cancellationToken)
    {
        var session = await sessions.LoginAsync(context, request, cancellationToken);
        return session is null ? ApiErrors.Result(context, 401, "invalid_credentials", "账号或密码错误。") : TypedResults.Ok(session);
    }

    /// <summary>读取当前管理员会话。</summary>
    /// <param name="context">当前请求。</param>
    /// <param name="sessions">会话服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>会话信息或未认证状态。</returns>
    private static async Task<Results<Ok<SessionResponse>, UnauthorizedHttpResult>> Session(
        HttpContext context, AdministratorSessionService sessions, CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(context.User, cancellationToken);
        return session is null ? TypedResults.Unauthorized() : TypedResults.Ok(session);
    }

    /// <summary>撤销当前会话并清除 Cookie。</summary>
    /// <param name="context">当前请求。</param>
    /// <param name="sessions">会话服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作完成状态。</returns>
    private static async Task<NoContent> Logout(HttpContext context, AdministratorSessionService sessions, CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(context, cancellationToken);
        return TypedResults.NoContent();
    }
}
