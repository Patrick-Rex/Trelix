using Microsoft.EntityFrameworkCore;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Projects;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Notifications;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Features.Releases;

/// <summary>负责不可变发布历史与受并发保护的发布事务。</summary>
/// <param name="db">当前请求数据库上下文。</param>
/// <param name="files">文件层级校验服务。</param>
/// <param name="time">UTC 时间来源。</param>
/// <param name="notifications">提交后的发布唤醒服务。</param>
/// <param name="logger">不包含正文的通知故障诊断。</param>
public sealed class ReleaseService(TrelixDbContext db, ConfigFileService files, TimeProvider time,
    IReleaseNotifications notifications, ILogger<ReleaseService> logger)
{
    /// <summary>分页查询指定文件的历史元数据，不加载历史正文。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>版本倒序排列的历史页。</returns>
    public async Task<PageResponse<ReleaseResponse>> ListAsync(Guid projectId, Guid environmentId, Guid fileId,
        int page, int pageSize, CancellationToken ct)
    {
        await files.FindAsync(projectId, environmentId, fileId, ct);
        return new(await db.Releases.Where(x => x.ConfigFileId == fileId).OrderByDescending(x => x.Version)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ReleaseResponse(x.ConfigFileId, x.Version, x.DraftRevision, x.SourceVersion, x.PublishedAt))
            .ToListAsync(ct), page, pageSize);
    }

    /// <summary>读取同一文件内指定版本的不可变正文。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="version">发布版本号。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>完整历史快照。</returns>
    public async Task<ReleaseDetailResponse> GetAsync(Guid projectId, Guid environmentId, Guid fileId, long version, CancellationToken ct)
    {
        await files.FindAsync(projectId, environmentId, fileId, ct);
        return await db.Releases.Where(x => x.ConfigFileId == fileId && x.Version == version)
            .Select(x => new ReleaseDetailResponse(new ReleaseResponse(x.ConfigFileId, x.Version, x.DraftRevision, x.SourceVersion, x.PublishedAt), x.Json))
            .SingleOrDefaultAsync(ct) ?? throw new ApiOperationException(404, "release_not_found", "历史发布版本不存在。");
    }

    /// <summary>以明确选中的当前草稿修订生成新发布版本。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">草稿修订与并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已提交的新版本和文件状态。</returns>
    public async Task<PublicationResponse> PublishAsync(Guid projectId, Guid environmentId, Guid fileId, PublishRequest request, CancellationToken ct)
    {
        var file = await files.FindAsync(projectId, environmentId, fileId, ct);
        ProjectService.RequireStamp(request.ConcurrencyStamp, file.ConcurrencyStamp);
        if (file.DraftJson is null)
            throw new ApiOperationException(409, "draft_missing", "文件尚无可发布的草稿。");
        if (request.DraftRevision != file.DraftRevision)
            throw new ApiOperationException(409, "draft_changed", "选中的草稿修订已变化，请重新核对。");
        ConfigJson.Validate(file.DraftJson);
        return await CommitAsync(file, file.DraftJson, file.DraftRevision, null, ct);
    }

    /// <summary>将历史正文发布为新版本，保留当前草稿及修订。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">源历史版本及并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>新发布版本和文件状态。</returns>
    public async Task<PublicationResponse> RollbackAsync(Guid projectId, Guid environmentId, Guid fileId, RollbackRequest request, CancellationToken ct)
    {
        var file = await files.FindAsync(projectId, environmentId, fileId, ct);
        ProjectService.RequireStamp(request.ConcurrencyStamp, file.ConcurrencyStamp);
        var source = await db.Releases.AsNoTracking().SingleOrDefaultAsync(x => x.ConfigFileId == fileId && x.Version == request.SourceVersion, ct)
            ?? throw new ApiOperationException(404, "release_not_found", "历史发布版本不存在。");
        ConfigJson.Validate(source.Json);
        return await CommitAsync(file, source.Json, source.DraftRevision, source.Version, ct);
    }

    /// <summary>先通过条件写入取得文件写权限，再分配版本并提交快照与当前指向。</summary>
    /// <param name="file">已核对调用方基准的文件。</param>
    /// <param name="json">已校验的发布正文。</param>
    /// <param name="revision">产生正文的草稿修订号。</param>
    /// <param name="sourceVersion">可选回滚来源版本。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>同一事务提交的文件状态和发布元数据。</returns>
    private async Task<PublicationResponse> CommitAsync(ConfigFile file, string json, long revision, long? sourceVersion, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // 此次 UPDATE 的 WHERE 包含原始 ConcurrencyStamp；事务内不会有第二个写者成功取得同一基准。
        db.Entry(file).Property(x => x.ConcurrencyStamp).IsModified = true;
        await db.SaveChangesAsync(ct);
        var previous = await db.Releases.Where(x => x.ConfigFileId == file.Id).MaxAsync(x => (long?)x.Version, ct) ?? 0;
        if (previous == long.MaxValue)
            throw new ApiOperationException(409, "version_exhausted", "发布版本已达到可用范围上限。");
        var release = new Release
        {
            ConfigFileId = file.Id, Version = previous + 1, Json = json, DraftRevision = revision,
            SourceVersion = sourceVersion, PublishedAt = time.GetUtcNow()
        };
        db.Releases.Add(release);
        await db.SaveChangesAsync(ct);
        file.CurrentReleaseVersion = release.Version;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        try
        {
            notifications.Notify(file.Id);
        }
        catch (Exception)
        {
            // 发布已经提交，不能以通知故障把成功写入伪装成失败，也不记录异常正文。
            logger.LogWarning("发布已经提交，但监听唤醒失败；客户端将在下次持久化核对时发现更新。");
        }
        return new(ConfigFileService.ToResponse(file),
            new ReleaseResponse(file.Id, release.Version, revision, sourceVersion, release.PublishedAt));
    }
}
