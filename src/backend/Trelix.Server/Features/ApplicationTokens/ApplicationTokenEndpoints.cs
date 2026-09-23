using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Trelix.Server.Features.ApplicationTokens;

/// <summary>映射管理员应用令牌生命周期端点。</summary>
public static class ApplicationTokenEndpoints
{
    /// <summary>注册令牌查询、签发、撤销和轮换端点。</summary>
    /// <param name="admin">已配置管理员策略的路由组。</param>
    public static void MapApplicationTokens(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup("/application-tokens").WithTags("ApplicationTokens");
        group.MapGet("/", List).WithSummary("分页查询令牌元数据，不返回凭证原文或校验摘要。");
        group.MapGet("/{id:guid}", Get).WithSummary("查询单个令牌的授权范围和生命周期。");
        group.MapPost("/", Create).WithSummary("创建应用只读令牌；原文仅本次返回。");
        group.MapPost("/{id:guid}/revoke", Revoke).WithSummary("立即撤销令牌；重复撤销不改变结果。");
        group.MapPost("/{id:guid}/rotate", Rotate).WithSummary("原子地撤销旧令牌并生成新令牌，沿用授权范围。");
    }

    /// <summary>分页读取不含凭证的令牌元数据。</summary>
    /// <param name="tokens">令牌服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <param name="page">从 1 开始的页码。</param>
    /// <param name="pageSize">每页最多 100 条。</param>
    /// <returns>令牌分页结果。</returns>
    private static async Task<Ok<ApplicationTokenListResponse>> List(ApplicationTokenService tokens,
        CancellationToken cancellationToken, [Range(1, 1000000)] int page = 1, [Range(1, 100)] int pageSize = 50) =>
        TypedResults.Ok(await tokens.ListAsync(page, pageSize, cancellationToken));

    /// <summary>读取指定令牌的元数据。</summary>
    /// <param name="id">令牌标识。</param>
    /// <param name="tokens">令牌服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>元数据或不存在状态。</returns>
    private static async Task<Results<Ok<ApplicationTokenResponse>, NotFound>> Get(Guid id,
        ApplicationTokenService tokens, CancellationToken cancellationToken)
    {
        var token = await tokens.GetAsync(id, cancellationToken);
        return token is null ? TypedResults.NotFound() : TypedResults.Ok(token);
    }

    /// <summary>签发指定授权范围的只读令牌。</summary>
    /// <param name="request">名称、有效期和授权范围。</param>
    /// <param name="tokens">令牌服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>一次性凭证及资源地址。</returns>
    private static async Task<Created<IssuedApplicationTokenResponse>> Create(CreateApplicationTokenRequest request,
        ApplicationTokenService tokens, CancellationToken cancellationToken)
    {
        var issued = await tokens.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/admin/application-tokens/{issued.Token.Id}", issued);
    }

    /// <summary>立即撤销令牌。</summary>
    /// <param name="id">令牌标识。</param>
    /// <param name="tokens">令牌服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>操作完成状态。</returns>
    private static async Task<NoContent> Revoke(Guid id, ApplicationTokenService tokens, CancellationToken cancellationToken)
    {
        await tokens.RevokeAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>原子撤销旧令牌并签发后继令牌。</summary>
    /// <param name="id">原令牌标识。</param>
    /// <param name="request">新有效期。</param>
    /// <param name="tokens">令牌服务。</param>
    /// <param name="cancellationToken">请求取消令牌。</param>
    /// <returns>一次性新凭证及资源地址。</returns>
    private static async Task<Created<IssuedApplicationTokenResponse>> Rotate(Guid id, RotateApplicationTokenRequest request,
        ApplicationTokenService tokens, CancellationToken cancellationToken)
    {
        var issued = await tokens.RotateAsync(id, request, cancellationToken);
        return TypedResults.Created($"/api/admin/application-tokens/{issued.Token.Id}", issued);
    }
}
