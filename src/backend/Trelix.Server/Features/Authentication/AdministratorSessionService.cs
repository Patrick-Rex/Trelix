using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Features.Authentication;

/// <summary>管理固定有效期的管理员会话及对应的登录 Cookie。</summary>
/// <param name="db">当前作用域的数据库上下文。</param>
/// <param name="passwords">管理员密码哈希生成与校验器。</param>
/// <param name="time">用于生命周期校验的时间提供程序。</param>
public sealed class AdministratorSessionService(TrelixDbContext db, IPasswordHasher<Administrator> passwords, TimeProvider time)
{
    /// <summary>校验密码、按需更新密码哈希，替换当前会话并写入登录 Cookie。</summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <param name="request">管理员登录凭证。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>登录成功的会话信息；凭证无效时为 null。</returns>
    public async Task<SessionResponse?> LoginAsync(HttpContext context, LoginRequest request, CancellationToken cancellationToken)
    {
        var admin = await db.Administrators.SingleAsync(cancellationToken);
        // Always verify the hash, including when the username does not match.
        var result = passwords.VerifyHashedPassword(admin, admin.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed || !string.Equals(admin.Username, request.Username, StringComparison.Ordinal))
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            admin.PasswordHash = passwords.HashPassword(admin, request.Password);

        var now = time.GetUtcNow();
        var previousId = Guid.TryParse(context.User.FindFirstValue(AuthenticationConstants.SessionClaim), out var id) ? id : Guid.Empty;
        await db.AdministratorSessions.Where(x => x.ExpiresAt <= now || x.Id == previousId).ExecuteDeleteAsync(cancellationToken);
        var session = new AdministratorSession { SecurityStamp = admin.SecurityStamp, ExpiresAt = now + AuthenticationConstants.SessionLifetime };
        db.AdministratorSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(AuthenticationConstants.SessionClaim, session.Id.ToString())
        ], AuthenticationConstants.AdministratorScheme);

        await context.SignInAsync(AuthenticationConstants.AdministratorScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IssuedUtc = now,
            ExpiresUtc = session.ExpiresAt,
            IsPersistent = false,
            AllowRefresh = false
        });
        return new SessionResponse(admin.Username, session.ExpiresAt);
    }

    /// <summary>读取已认证身份对应的未过期会话。</summary>
    /// <param name="user">已通过管理员认证的请求身份。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>当前会话信息；会话不存在或已过期时为 null。</returns>
    public async Task<SessionResponse?> GetAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var sessionId = Guid.Parse(user.FindFirstValue(AuthenticationConstants.SessionClaim)!);
        var now = time.GetUtcNow();
        var expiry = await db.AdministratorSessions.Where(x => x.Id == sessionId && x.ExpiresAt > now)
            .Select(x => (DateTimeOffset?)x.ExpiresAt).SingleOrDefaultAsync(cancellationToken);
        return expiry is null ? null : new SessionResponse(user.Identity!.Name!, expiry.Value);
    }

    /// <summary>删除当前持久化会话并清除登录 Cookie。</summary>
    /// <param name="context">当前 HTTP 请求上下文。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>表示退出完成的任务。</returns>
    public async Task LogoutAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var sessionId = Guid.Parse(context.User.FindFirstValue(AuthenticationConstants.SessionClaim)!);
        await db.AdministratorSessions.Where(x => x.Id == sessionId).ExecuteDeleteAsync(cancellationToken);
        await context.SignOutAsync(AuthenticationConstants.AdministratorScheme);
    }
}
