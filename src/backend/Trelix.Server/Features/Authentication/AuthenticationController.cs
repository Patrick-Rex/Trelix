using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Features.Authentication;

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
    [AllowAnonymous]
    [HttpGet("antiforgery")]
    [EndpointSummary("获取防伪造令牌；登录前及登录后分别获取。")]
    [ProducesResponseType<AntiforgeryResponse>(200)]
    public ActionResult<AntiforgeryResponse> Antiforgery(CancellationToken cancellationToken)
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new AntiforgeryResponse(tokens.RequestToken!, AuthenticationConstants.CsrfHeader));
    }

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

    [HttpGet("session")]
    [EndpointSummary("查询当前管理员会话及到期时间。")]
    [ProducesResponseType<SessionResponse>(200)]
    public async Task<ActionResult<SessionResponse>> Session(CancellationToken cancellationToken)
    {
        var session = await sessions.GetAsync(User, cancellationToken);
        return session is null ? Unauthorized() : Ok(session);
    }

    [HttpPost("logout")]
    [EndpointSummary("使当前会话立即失效并清除登录 Cookie。")]
    [ProducesResponseType(204)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await sessions.LogoutAsync(HttpContext, cancellationToken);
        return NoContent();
    }
}
