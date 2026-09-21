using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Features.ApplicationTokens;

/// <summary>提供管理员应用令牌管理 API，凭证原文仅在签发时返回。</summary>
/// <param name="tokens">应用令牌用例服务。</param>
[ApiController]
[Route("api/admin/application-tokens")]
[ApiExplorerSettings(GroupName = "admin")]
[Authorize(Policy = AuthenticationConstants.AdministratorPolicy)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
[ProducesResponseType<ProblemDetails>(400)]
[ProducesResponseType<ProblemDetails>(401)]
[ProducesResponseType<ProblemDetails>(403)]
[ProducesResponseType<ProblemDetails>(409)]
public sealed class ApplicationTokensController(ApplicationTokenService tokens) : ControllerBase
{
    /// <summary>按页查询令牌元数据，不返回原文或校验摘要。</summary>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <param name="page">从 1 开始的页码。</param>
    /// <param name="pageSize">每页返回的最大条目数。</param>
    /// <returns>包含令牌列表及分页信息的成功响应。</returns>
    [HttpGet]
    [EndpointSummary("分页查询令牌元数据，不返回凭证原文或校验摘要。")]
    [ProducesResponseType<ApplicationTokenListResponse>(200)]
    public async Task<ActionResult<ApplicationTokenListResponse>> List(CancellationToken cancellationToken,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 50) =>
        Ok(await tokens.ListAsync(page, pageSize, cancellationToken));

    /// <summary>查询单个令牌的授权范围及生命周期。</summary>
    /// <param name="id">目标应用令牌标识。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>令牌元数据；不存在时返回 404。</returns>
    [HttpGet("{id:guid}")]
    [EndpointSummary("查询单个令牌的授权范围和生命周期。")]
    [ProducesResponseType<ApplicationTokenResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<ApplicationTokenResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAsync(id, cancellationToken);
        return token is null ? NotFound() : Ok(token);
    }

    /// <summary>创建限定项目与环境范围的应用只读令牌。</summary>
    /// <param name="request">包含名称、有效期和授权范围的创建请求。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>包含一次性凭证原文和资源位置的 201 响应。</returns>
    [HttpPost]
    [EndpointSummary("创建应用只读令牌；原文仅本次返回。")]
    [ProducesResponseType<IssuedApplicationTokenResponse>(201)]
    public async Task<ActionResult<IssuedApplicationTokenResponse>> Create(CreateApplicationTokenRequest request, CancellationToken cancellationToken)
    {
        var issued = await tokens.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = issued.Token.Id }, issued);
    }

    /// <summary>撤销指定令牌，重复撤销保持幂等。</summary>
    /// <param name="id">目标应用令牌标识。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>撤销完成后的 204 响应。</returns>
    [HttpPost("{id:guid}/revoke")]
    [EndpointSummary("立即撤销令牌；重复撤销不改变结果。")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await tokens.RevokeAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>在同一事务中撤销旧令牌并签发沿用授权范围的新令牌。</summary>
    /// <param name="id">目标应用令牌标识。</param>
    /// <param name="request">指定新令牌到期时间的轮换请求。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>包含新凭证原文和资源位置的 201 响应。</returns>
    [HttpPost("{id:guid}/rotate")]
    [EndpointSummary("原子地撤销旧令牌并生成新令牌，沿用授权范围。")]
    [ProducesResponseType<IssuedApplicationTokenResponse>(201)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<IssuedApplicationTokenResponse>> Rotate(Guid id, RotateApplicationTokenRequest request, CancellationToken cancellationToken)
    {
        var issued = await tokens.RotateAsync(id, request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = issued.Token.Id }, issued);
    }
}
