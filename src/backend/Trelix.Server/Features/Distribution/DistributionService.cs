using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Infrastructure.Notifications;
using Trelix.Server.Persistence;

namespace Trelix.Server.Features.Distribution;

/// <summary>应用读取的一份已发布快照，正文与身份来自同一发布记录。</summary>
/// <param name="ConfigFileId">文件稳定标识，删除后同名重建不会复用。</param>
/// <param name="Version">该文件的发布版本号。</param>
/// <param name="PublishedAt">UTC 发布时间。</param>
/// <param name="Json">已发布的 JSON 原文。</param>
public sealed record PublishedConfigResponse(Guid ConfigFileId, long Version, DateTimeOffset PublishedAt, string Json);

/// <summary>当前发布的不可变身份，不包含配置正文。</summary>
/// <param name="ConfigFileId">文件稳定标识。</param>
/// <param name="Version">该文件的发布版本号。</param>
public sealed record PublishedIdentityResponse(Guid ConfigFileId, long Version);

/// <summary>解析资源名称并实时授权，只从当前发布记录投影应用配置。</summary>
/// <param name="scopes">短数据库作用域工厂。</param>
/// <param name="notifications">有界的发布等待管理器。</param>
/// <param name="options">监听等待配置。</param>
/// <param name="time">生命周期及等待时间来源。</param>
/// <param name="lifetime">服务停止信号。</param>
public sealed class DistributionService(IServiceScopeFactory scopes, IReleaseNotifications notifications,
    IOptions<DistributionOptions> options, TimeProvider time, IHostApplicationLifetime lifetime)
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
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrelixDbContext>();
        var resource = await ResolveAsync(scope.ServiceProvider, db, user, projectKey, environmentKey, ct);
        return await (from file in db.ConfigFiles
                      join release in db.Releases on new { ConfigFileId = file.Id, Version = file.CurrentReleaseVersion }
                          equals new { release.ConfigFileId, Version = (long?)release.Version }
                      where file.EnvironmentId == resource.EnvironmentId && file.Name == fileName
                      select new PublishedConfigResponse(file.Id, release.Version, release.PublishedAt, release.Json))
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiOperationException(404, "published_config_not_found", "文件不存在或尚未发布。");
    }

    /// <summary>核对、注册、再次核对并等待发布，等待期间不持有数据库资源。</summary>
    /// <param name="user">已认证的应用身份。</param>
    /// <param name="projectKey">项目业务标识。</param>
    /// <param name="environmentKey">环境业务标识。</param>
    /// <param name="fileName">文件原名。</param>
    /// <param name="known">调用方最近成功加载的发布身份。</param>
    /// <param name="ct">请求取消信号。</param>
    /// <returns>变化后的发布身份；正常等待结束且未变化时为 null。</returns>
    public async Task<PublishedIdentityResponse?> WatchAsync(ClaimsPrincipal user, string projectKey, string environmentKey,
        string fileName, PublishedIdentityResponse known, CancellationToken ct)
    {
        if (known.ConfigFileId == Guid.Empty || known.Version <= 0)
            throw new ApiOperationException(400, "invalid_release_identity", "已知发布身份无效。");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.ApplicationStopping);
        var current = await ReadIdentityAsync(user, projectKey, environmentKey, fileName, stop.Token);
        if (current != known)
            return current;
        using var subscription = notifications.Subscribe(current.ConfigFileId);
        current = await ReadIdentityAsync(user, projectKey, environmentKey, fileName, stop.Token);
        if (current != known)
            return current;
        try
        {
            await subscription.Signal.WaitAsync(options.Value.WaitTimeout, time, stop.Token);
        }
        catch (TimeoutException)
        {
            // 超时仍查询持久化身份，覆盖提交成功但未能通知的发布。
        }
        current = await ReadIdentityAsync(user, projectKey, environmentKey, fileName, stop.Token);
        return current == known ? null : current;
    }

    /// <summary>只投影发布身份，并在返回前重新核对当前令牌及资源授权。</summary>
    /// <param name="user">应用身份。</param>
    /// <param name="projectKey">项目业务标识。</param>
    /// <param name="environmentKey">环境业务标识。</param>
    /// <param name="fileName">文件原名。</param>
    /// <param name="ct">取消信号。</param>
    /// <returns>来自数据库当前发布记录的身份。</returns>
    private async Task<PublishedIdentityResponse> ReadIdentityAsync(ClaimsPrincipal user, string projectKey,
        string environmentKey, string fileName, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TrelixDbContext>();
        var resource = await ResolveAsync(scope.ServiceProvider, db, user, projectKey, environmentKey, ct);
        return await (from file in db.ConfigFiles
                      join release in db.Releases on new { ConfigFileId = file.Id, Version = file.CurrentReleaseVersion }
                          equals new { release.ConfigFileId, Version = (long?)release.Version }
                      where file.EnvironmentId == resource.EnvironmentId && file.Name == fileName
                      select new PublishedIdentityResponse(file.Id, release.Version))
            .SingleOrDefaultAsync(ct)
            ?? throw new ApiOperationException(404, "published_config_not_found", "文件不存在或尚未发布。");
    }

    /// <summary>先重新校验令牌生命周期，再解析名称并核对授权，区分 401 与 403。</summary>
    /// <param name="services">本次短作用域的服务。</param>
    /// <param name="db">本次查询的数据库上下文。</param>
    /// <param name="user">应用身份。</param>
    /// <param name="projectKey">项目业务标识。</param>
    /// <param name="environmentKey">环境业务标识。</param>
    /// <param name="ct">取消信号。</param>
    /// <returns>已获授权的项目环境身份。</returns>
    private async Task<ApplicationResource> ResolveAsync(IServiceProvider services, TrelixDbContext db, ClaimsPrincipal user,
        string projectKey, string environmentKey, CancellationToken ct)
    {
        var identity = user.Identities.SingleOrDefault(x => x.AuthenticationType == AuthenticationConstants.ApplicationScheme);
        var now = time.GetUtcNow();
        if (!Guid.TryParse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var tokenId)
            || !await db.ApplicationTokens.AnyAsync(x => x.Id == tokenId && x.RevokedAt == null && x.ExpiresAt > now, ct))
            throw new ApiOperationException(401, "authentication_required", "应用令牌无效或已失效。");
        var resource = await (from environment in db.Environments
                              join project in db.Projects on environment.ProjectId equals project.Id
                              where project.Key == projectKey && environment.Key == environmentKey
                              select new ApplicationResource(project.Id, environment.Id)).SingleOrDefaultAsync(ct)
            ?? throw new ApiOperationException(404, "resource_not_found", "项目或环境不存在。");
        var access = await services.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, resource, new ApplicationScopeRequirement());
        if (!access.Succeeded)
            throw new ApiOperationException(403, "access_denied", "应用令牌无权访问此项目环境。");

        return resource;
    }
}
