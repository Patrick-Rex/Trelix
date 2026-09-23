using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;

namespace Trelix.Server.Features.Distribution;

/// <summary>应用读取的一份已发布快照，正文与身份来自同一发布记录。</summary>
/// <param name="ConfigFileId">文件稳定标识，删除后同名重建不会复用。</param>
/// <param name="Version">该文件的发布版本号。</param>
/// <param name="PublishedAt">UTC 发布时间。</param>
/// <param name="Json">已发布的 JSON 原文。</param>
public sealed record PublishedConfigResponse(Guid ConfigFileId, long Version, DateTimeOffset PublishedAt, string Json);

/// <summary>解析资源名称并实时授权，只从当前发布记录投影应用配置。</summary>
/// <param name="db">当前请求数据库上下文。</param>
/// <param name="authorization">应用资源授权服务。</param>
public sealed class DistributionService(TrelixDbContext db, IAuthorizationService authorization)
{
    /// <summary>读取所选文件的当前已发布内容，绝不返回草稿。</summary>
    /// <param name="user">已通过应用令牌认证的身份。</param>
    /// <param name="projectKey">项目业务标识。</param>
    /// <param name="environmentKey">环境业务标识。</param>
    /// <param name="fileName">文件名。</param>
    /// <param name="ct">请求取消令牌。</param>
    /// <returns>正文与身份一致的发布快照。</returns>
    public async Task<PublishedConfigResponse> ReadAsync(ClaimsPrincipal user, string projectKey, string environmentKey, string fileName, CancellationToken ct)
    {
        var resource = await (from environment in db.Environments
                              join project in db.Projects on environment.ProjectId equals project.Id
                              where project.Key == projectKey && environment.Key == environmentKey
                              select new ApplicationResource(project.Id, environment.Id)).SingleOrDefaultAsync(ct)
            ?? throw new ApiOperationException(404, "resource_not_found", "项目或环境不存在。");
        var access = await authorization.AuthorizeAsync(user, resource, new ApplicationScopeRequirement());
        if (!access.Succeeded)
            throw new ApiOperationException(403, "access_denied", "应用令牌无权访问此项目环境。");

        return await (from file in db.ConfigFiles
                      join release in db.Releases on new { ConfigFileId = file.Id, Version = file.CurrentReleaseVersion }
                          equals new { release.ConfigFileId, Version = (long?)release.Version }
                      where file.EnvironmentId == resource.EnvironmentId && file.Name == fileName
                      select new PublishedConfigResponse(file.Id, release.Version, release.PublishedAt, release.Json))
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiOperationException(404, "published_config_not_found", "文件不存在或尚未发布。");
    }
}
