using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Features.ApplicationTokens;

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
    [HttpGet]
    [EndpointSummary("分页查询令牌元数据，不返回凭证原文或校验摘要。")]
    [ProducesResponseType<ApplicationTokenListResponse>(200)]
    public async Task<ActionResult<ApplicationTokenListResponse>> List(CancellationToken cancellationToken,
        [FromQuery, Range(1, 1000000)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 50) =>
        Ok(await tokens.ListAsync(page, pageSize, cancellationToken));

    [HttpGet("{id:guid}")]
    [EndpointSummary("查询单个令牌的授权范围和生命周期。")]
    [ProducesResponseType<ApplicationTokenResponse>(200)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<ActionResult<ApplicationTokenResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAsync(id, cancellationToken);
        return token is null ? NotFound() : Ok(token);
    }

    [HttpPost]
    [EndpointSummary("创建应用只读令牌；原文仅本次返回。")]
    [ProducesResponseType<IssuedApplicationTokenResponse>(201)]
    public async Task<ActionResult<IssuedApplicationTokenResponse>> Create(CreateApplicationTokenRequest request, CancellationToken cancellationToken)
    {
        var issued = await tokens.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = issued.Token.Id }, issued);
    }

    [HttpPost("{id:guid}/revoke")]
    [EndpointSummary("立即撤销令牌；重复撤销不改变结果。")]
    [ProducesResponseType(204)]
    [ProducesResponseType<ProblemDetails>(404)]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        await tokens.RevokeAsync(id, cancellationToken);
        return NoContent();
    }

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
