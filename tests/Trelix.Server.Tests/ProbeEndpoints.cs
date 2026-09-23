using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Tests;

/// <summary>仅在测试宿主注册用于验证认证方案与安全错误响应的探针。</summary>
public sealed class ProbeEndpoints : IStartupFilter
{
    /// <summary>在宿主装配时将测试专用端点加入路由集合。</summary>
    /// <param name="next">宿主后续装配动作。</param>
    /// <returns>包含探针注册的装配动作。</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet("/__tests/application/{projectId:guid}/{environmentId:guid}", Resource)
                .RequireAuthorization(AuthenticationConstants.ApplicationPolicy).ExcludeFromDescription();
            endpoints.MapGet("/api/admin/__tests/failure", Failure)
                .RequireAuthorization(AuthenticationConstants.AdministratorPolicy).ExcludeFromDescription();
        });
    };

    /// <summary>校验应用对指定项目环境的实时权限。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="context">当前请求。</param>
    /// <param name="authorization">资源授权服务。</param>
    /// <returns>授权成功或禁止访问状态。</returns>
    private static async Task<Results<NoContent, ForbidHttpResult>> Resource(Guid projectId, Guid environmentId,
        HttpContext context, IAuthorizationService authorization)
    {
        var result = await authorization.AuthorizeAsync(context.User, new ApplicationResource(projectId, environmentId), new ApplicationScopeRequirement());
        return result.Succeeded ? TypedResults.NoContent() : TypedResults.Forbid(authenticationSchemes: [AuthenticationConstants.ApplicationScheme]);
    }

    /// <summary>抛出含敏感标记的异常供信息边界测试使用。</summary>
    /// <returns>此方法始终抛出异常。</returns>
    private static IResult Failure() => throw new InvalidOperationException("sensitive-internal-body");
}
