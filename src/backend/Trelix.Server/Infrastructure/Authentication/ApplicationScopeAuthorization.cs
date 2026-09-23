using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>应用令牌授权校验所针对的项目与环境组合。</summary>
/// <param name="ProjectId">目标项目标识。</param>
/// <param name="EnvironmentId">目标环境标识。</param>
public sealed record ApplicationResource(Guid ProjectId, Guid EnvironmentId);
/// <summary>要求应用令牌当前仍有效且拥有目标项目环境的访问授权。</summary>
public sealed class ApplicationScopeRequirement : IAuthorizationRequirement;

// Distribution endpoints must use this resource requirement after ApplicationPolicy.
// Re-evaluate after long polling; claims never cache grants or lifecycle state.
/// <summary>实时查询令牌生命周期和资源范围，避免缓存授权导致撤销失效。</summary>
/// <param name="scopes">创建短数据库作用域的工厂。</param>
/// <param name="time">用于生命周期校验的时间提供程序。</param>
/// <param name="httpContextAccessor">用于读取当前请求取消令牌的上下文访问器。</param>
public sealed class ApplicationScopeHandler(IServiceScopeFactory scopes, TimeProvider time, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ApplicationScopeRequirement, ApplicationResource>
{
    /// <summary>验证令牌未过期、未撤销且匹配项目环境；满足时标记要求成功。</summary>
    /// <param name="context">包含当前身份及授权状态的上下文。</param>
    /// <param name="requirement">当前待满足的资源访问要求。</param>
    /// <param name="resource">被访问的项目和环境组合。</param>
    /// <returns>表示授权校验完成的任务。</returns>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApplicationScopeRequirement requirement, ApplicationResource resource)
    {
        var identity = context.User.Identities.SingleOrDefault(x => x.AuthenticationType == AuthenticationConstants.ApplicationScheme);
        if (!Guid.TryParse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var tokenId))
            return;

        var now = time.GetUtcNow();
        await using var databaseScope = scopes.CreateAsyncScope();
        var db = databaseScope.ServiceProvider.GetRequiredService<TrelixDbContext>();
        var allowed = await (from scope in db.TokenScopes
                             join environment in db.Environments on scope.EnvironmentId equals environment.Id
                             join token in db.ApplicationTokens on scope.ApplicationTokenId equals token.Id
                             where token.Id == tokenId && token.RevokedAt == null && token.ExpiresAt > now
                                 && environment.Id == resource.EnvironmentId && environment.ProjectId == resource.ProjectId
                             select token.Id).AnyAsync(httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        if (allowed)
            context.Succeed(requirement);
    }
}
