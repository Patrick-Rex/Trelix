using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Tests;

// Registered only by ServerFactory. These routes prove M2's schemes and resource policy,
// and do not stand in for the M3/M5 production distribution endpoints.
/// <summary>仅供测试验证应用认证和资源范围授权的探针。</summary>
/// <param name="authorization">资源授权校验服务。</param>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = AuthenticationConstants.ApplicationPolicy)]
[Route("__tests/application")]
public sealed class ApplicationProbeController(IAuthorizationService authorization) : ControllerBase
{
    /// <summary>验证当前应用身份对指定项目环境的读取权限。</summary>
    /// <param name="projectId">目标项目标识。</param>
    /// <param name="environmentId">目标环境标识。</param>
    /// <returns>授权成功返回 204，否则返回 403。</returns>
    [HttpGet("{projectId:guid}/{environmentId:guid}")]
    public async Task<IActionResult> Resource(Guid projectId, Guid environmentId)
    {
        var result = await authorization.AuthorizeAsync(User, new ApplicationResource(projectId, environmentId), new ApplicationScopeRequirement());
        return result.Succeeded ? NoContent() : Forbid(AuthenticationConstants.ApplicationScheme);
    }
}

/// <summary>仅供测试验证管理 API 异常处理与信息边界的探针。</summary>
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = AuthenticationConstants.AdministratorPolicy)]
[Route("api/admin/__tests")]
public sealed class ManagementProbeController : ControllerBase
{
    /// <summary>抛出含敏感标记的测试异常，以验证响应与日志不会泄露异常正文。</summary>
    /// <returns>此方法始终抛出异常，不返回结果。</returns>
    [HttpGet("failure")]
    public IActionResult Failure() => throw new InvalidOperationException("sensitive-internal-body");
}
