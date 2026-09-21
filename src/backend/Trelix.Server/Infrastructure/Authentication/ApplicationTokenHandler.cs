using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trelix.Server.Persistence;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>通过 Bearer 原文的 SHA-256 摘要校验只读应用身份。</summary>
/// <param name="options">应用令牌认证方案的配置监视器。</param>
/// <param name="logger">诊断日志服务。</param>
/// <param name="encoder">认证处理器使用的 URL 编码器。</param>
/// <param name="db">当前作用域的数据库上下文。</param>
/// <param name="time">用于生命周期校验的时间提供程序。</param>
public sealed class ApplicationTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TrelixDbContext db,
    TimeProvider time) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>验证请求头格式及令牌生命周期，建立仅含令牌标识的身份。</summary>
    /// <returns>身份认证结果；缺少请求头时不产生认证结果。</returns>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values))
            return AuthenticateResult.NoResult();
        if (values.Count != 1 || !AuthenticationHeaderValue.TryParse(values[0], out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || header.Parameter is not { } secret || !ApplicationTokenSecret.HasValidFormat(secret))
            return AuthenticateResult.Fail("Invalid application credential.");

        var hash = ApplicationTokenSecret.Hash(secret);
        var now = time.GetUtcNow();
        var id = await db.ApplicationTokens.Where(x => x.SecretHash == hash && x.RevokedAt == null && x.ExpiresAt > now)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(Context.RequestAborted);
        if (id is null)
            return AuthenticateResult.Fail("Invalid application credential.");

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.Value.ToString())], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    /// <summary>返回 401 并声明 Bearer 认证挑战。</summary>
    /// <param name="properties">认证挑战的附加属性。</param>
    /// <returns>已完成的任务。</returns>
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
