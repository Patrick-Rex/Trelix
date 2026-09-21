using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>逐请求验证持久化管理员会话，并将认证跳转转换为 API 状态码。</summary>
/// <param name="db">当前作用域的数据库上下文。</param>
/// <param name="time">用于生命周期校验的时间提供程序。</param>
public sealed class AdministratorCookieEvents(TrelixDbContext db, TimeProvider time) : CookieAuthenticationEvents
{
    /// <summary>校验会话有效期及安全戳；失效时拒绝身份并清除 Cookie。</summary>
    /// <param name="context">包含 Cookie 身份的验证上下文。</param>
    /// <returns>表示身份校验完成的任务。</returns>
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var now = time.GetUtcNow();
        if (!Guid.TryParse(context.Principal?.FindFirstValue(AuthenticationConstants.SessionClaim), out var sessionId)
            || context.Principal.FindFirstValue(ClaimTypes.NameIdentifier) != "1"
            || !await (from session in db.AdministratorSessions
                       join admin in db.Administrators on session.AdministratorId equals admin.Id
                       where session.Id == sessionId && session.ExpiresAt > now && session.SecurityStamp == admin.SecurityStamp
                       select session.Id).AnyAsync(context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthenticationConstants.AdministratorScheme);
        }
    }

    /// <summary>将登录挑战转换为 401，不跳转到登录页面。</summary>
    /// <param name="context">Cookie 登录挑战的重定向上下文。</param>
    /// <returns>已完成的任务。</returns>
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    /// <summary>将访问拒绝转换为 403，不跳转到错误页面。</summary>
    /// <param name="context">Cookie 访问拒绝的重定向上下文。</param>
    /// <returns>已完成的任务。</returns>
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
