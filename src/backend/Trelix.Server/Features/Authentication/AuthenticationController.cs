using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Features.Authentication;

/// <summary>提供管理员登录、会话查询、退出和防伪造令牌 API。</summary>
/// <param name="sessions">管理员会话用例服务。</param>
/// <param name="antiforgery">ASP.NET Core 防伪造服务。</param>
[ApiController]
[Route("api/admin/auth")]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Policy = AuthenticationConstants.AdministratorPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
public sealed class AuthenticationController(AdministratorSessionService sessions, IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>签发与当前身份绑定的防伪造请求令牌，并按需写入配套 Cookie。</summary>
    /// <param name="cancellationToken">请求取消令牌；底层令牌生成接口为同步调用。</param>
    /// <returns>请求令牌及客户端应使用的请求头名称。</returns>
    [AllowAnonymous]
    [HttpGet("antiforgery")]
    [EndpointSummary("获取防伪造令牌；登录前及登录后分别获取。")]
    [ProducesResponseType<AntiforgeryResponse>(200)]
    public ActionResult<AntiforgeryResponse> Antiforgery(CancellationToken cancellationToken)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryResponse(tokens.RequestToken!, AuthenticationConstants.CsrfHeader));
    }

    /// <summary>验证管理员凭证并建立固定有效期的 Cookie 会话。</summary>
    /// <param name="request">管理员登录凭证。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>会话信息；凭证无效时返回 401。</returns>
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    [EndpointSummary("验证管理员凭证并建立固定有效期的 Cookie 会话。")]
    [ProducesResponseType<SessionResponse>(200)]
    [ProducesResponseType<ProblemDetails>(429)]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var session = await sessions.LoginAsync(HttpContext, request, cancellationToken);
        return session is null ? ApiErrors.Result(HttpContext, 401, "invalid_credentials", "账号或密码错误。") : Ok(session);
    }

    /// <summary>查询当前管理员会话及到期时间。</summary>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>会话信息；会话失效时返回 401。</returns>
    [HttpGet("session")]
    [EndpointSummary("查询当前管理员会话及到期时间。")]
    [ProducesResponseType<SessionResponse>(200)]
    public async Task<ActionResult<SessionResponse>> Session(CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(User, cancellationToken);
        return session is null ? Unauthorized() : Ok(session);
    }

    /// <summary>立即使当前会话失效并清除登录 Cookie。</summary>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>退出完成后的 204 响应。</returns>
    [HttpPost("logout")]
    [EndpointSummary("使当前会话立即失效并清除登录 Cookie。")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(HttpContext, cancellationToken);
        return NoContent();
    }
}
