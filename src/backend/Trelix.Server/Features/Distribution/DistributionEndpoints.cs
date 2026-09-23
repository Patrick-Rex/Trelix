using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Features.Distribution;

/// <summary>映射与管理 API 分离的应用只读端点。</summary>
public static class DistributionEndpoints
{
    /// <summary>注册应用读取路由及独立认证、OpenAPI 分组。</summary>
    /// <param name="app">Server 路由构建器。</param>
    public static void MapDistribution(this WebApplication app)
    {
        app.MapGet("/api/application/configuration", Read)
            .WithGroupName("application").WithTags("Distribution")
            .RequireAuthorization(AuthenticationConstants.ApplicationPolicy)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404)
            .WithSummary("按项目、环境及文件名读取授权范围内的当前已发布配置。");
        app.MapGet("/api/application/configuration/watch", Watch)
            .WithGroupName("application").WithTags("Distribution")
            .RequireAuthorization(AuthenticationConstants.ApplicationPolicy)
            .ProducesProblem(400).ProducesProblem(401).ProducesProblem(403).ProducesProblem(404).ProducesProblem(503)
            .WithSummary("等待所选文件的发布身份变化；正常等待结束且未变化时返回 204。");
    }

    /// <summary>通过应用身份读取一致的发布快照。</summary>
    /// <param name="projectKey">项目业务标识。</param>
    /// <param name="environmentKey">环境业务标识。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="context">当前请求身份。</param>
    /// <param name="service">配置分发服务。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>当前已发布快照。</returns>
    private static async Task<Ok<PublishedConfigResponse>> Read([Required, MaxLength(128)] string projectKey,
        [Required, MaxLength(128)] string environmentKey, [Required, MaxLength(256)] string fileName,
        HttpContext context, DistributionService service, CancellationToken ct) =>
        TypedResults.Ok(await service.ReadAsync(context.User, projectKey, environmentKey, fileName, ct));

    /// <summary>监听指定发布身份之后的变化，不返回配置正文。</summary>
    /// <param name="projectKey">项目业务标识。</param>
    /// <param name="environmentKey">环境业务标识。</param>
    /// <param name="fileName">配置文件原名。</param>
    /// <param name="knownConfigFileId">最近成功加载的文件标识。</param>
    /// <param name="knownVersion">最近成功加载的发布版本。</param>
    /// <param name="context">当前应用身份。</param>
    /// <param name="service">配置分发服务。</param>
    /// <param name="ct">请求取消信号。</param>
    /// <returns>变化后的身份或正常等待结束的空响应。</returns>
    private static async Task<Results<Ok<PublishedIdentityResponse>, NoContent>> Watch(
        [Required, MaxLength(128)] string projectKey, [Required, MaxLength(128)] string environmentKey,
        [Required, MaxLength(256)] string fileName, Guid knownConfigFileId, [Range(1, long.MaxValue)] long knownVersion,
        HttpContext context, DistributionService service, CancellationToken ct)
    {
        var changed = await service.WatchAsync(context.User, projectKey, environmentKey, fileName,
            new(knownConfigFileId, knownVersion), ct);
        return changed is null ? TypedResults.NoContent() : TypedResults.Ok(changed);
    }
}
