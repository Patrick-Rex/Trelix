using Microsoft.EntityFrameworkCore;
using Trelix.Server.Features.Projects;
using Trelix.Server.Infrastructure;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Features.ConfigFiles;

/// <summary>维护文件身份、草稿隔离以及受并发保护的整文件删除。</summary>
/// <param name="db">当前请求数据库上下文。</param>
/// <param name="projects">项目环境层级校验服务。</param>
public sealed class ConfigFileService(TrelixDbContext db, ProjectService projects)
{
    /// <summary>按文件名稳定分页读取元数据，不加载配置正文。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>文件分页结果。</returns>
    public async Task<PageResponse<ConfigFileResponse>> ListAsync(Guid projectId, Guid environmentId, int page, int pageSize, CancellationToken ct)
    {
        await projects.GetEnvironmentAsync(projectId, environmentId, ct);
        return new(await db.ConfigFiles.Where(x => x.EnvironmentId == environmentId).OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ConfigFileResponse(x.Id, x.EnvironmentId, x.Name, x.DraftRevision, x.CurrentReleaseVersion, x.ConcurrencyStamp))
            .ToListAsync(ct), page, pageSize);
    }

    /// <summary>读取文件与草稿的同一持久化快照。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>文件与草稿快照。</returns>
    public async Task<DraftResponse> GetAsync(Guid projectId, Guid environmentId, Guid fileId, CancellationToken ct)
    {
        var file = await FindAsync(projectId, environmentId, fileId, ct);
        return new(ToResponse(file), file.DraftJson);
    }

    /// <summary>在指定环境创建没有草稿与发布历史的文件。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="request">新文件名。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>新文件元数据。</returns>
    public async Task<ConfigFileResponse> CreateAsync(Guid projectId, Guid environmentId, CreateConfigFileRequest request, CancellationToken ct)
    {
        await projects.GetEnvironmentAsync(projectId, environmentId, ct);
        var file = new ConfigFile { EnvironmentId = environmentId, Name = request.Name };
        db.ConfigFiles.Add(file);
        await db.SaveChangesAsync(ct);
        return ToResponse(file);
    }

    /// <summary>按并发基准重命名文件，保留所有配置状态。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">新文件名和基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>更新后的文件元数据。</returns>
    public async Task<ConfigFileResponse> RenameAsync(Guid projectId, Guid environmentId, Guid fileId, RenameConfigFileRequest request, CancellationToken ct)
    {
        var file = await FindAsync(projectId, environmentId, fileId, ct);
        ProjectService.RequireStamp(request.ConcurrencyStamp, file.ConcurrencyStamp);
        file.Name = request.Name;
        db.Entry(file).Property(x => x.ConcurrencyStamp).IsModified = true;
        await db.SaveChangesAsync(ct);
        return ToResponse(file);
    }

    /// <summary>保存新的草稿修订，不修改当前发布指向。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="request">JSON 正文与并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>保存后的草稿状态。</returns>
    public async Task<DraftResponse> SaveDraftAsync(Guid projectId, Guid environmentId, Guid fileId, SaveDraftRequest request, CancellationToken ct)
    {
        ConfigJson.Validate(request.Json);
        var file = await FindAsync(projectId, environmentId, fileId, ct);
        ProjectService.RequireStamp(request.ConcurrencyStamp, file.ConcurrencyStamp);
        if (file.DraftRevision == long.MaxValue)
            throw new ApiOperationException(409, "revision_exhausted", "草稿修订已达到可用范围上限。");
        file.DraftJson = request.Json;
        file.DraftRevision++;
        await db.SaveChangesAsync(ct);
        return new(ToResponse(file), file.DraftJson);
    }

    /// <summary>在同一事务中解除当前指向并删除全部历史、草稿和文件。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="stamp">调用方并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>删除任务。</returns>
    public async Task DeleteAsync(Guid projectId, Guid environmentId, Guid fileId, Guid stamp, CancellationToken ct)
    {
        var file = await FindAsync(projectId, environmentId, fileId, ct);
        ProjectService.RequireStamp(stamp, file.ConcurrencyStamp);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        file.CurrentReleaseVersion = null;
        // 即使尚未发布，也强制在 SQL 写入条件中校验并发基准。
        db.Entry(file).Property(x => x.ConcurrencyStamp).IsModified = true;
        await db.SaveChangesAsync(ct);
        await db.Releases.Where(x => x.ConfigFileId == fileId).ExecuteDeleteAsync(ct);
        db.ConfigFiles.Remove(file);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    /// <summary>通过完整资源层级定位供写操作使用的文件。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>跟踪的文件实体，仅供 Server 用例协作。</returns>
    internal async Task<ConfigFile> FindAsync(Guid projectId, Guid environmentId, Guid fileId, CancellationToken ct) =>
        await (from file in db.ConfigFiles
               join environment in db.Environments on file.EnvironmentId equals environment.Id
               where file.Id == fileId && environment.Id == environmentId && environment.ProjectId == projectId
               select file).SingleOrDefaultAsync(ct)
        ?? throw new ApiOperationException(404, "file_not_found", "配置文件不存在或不属于指定项目环境。");

    /// <summary>将持久化文件映射为不含正文的响应。</summary>
    /// <param name="file">待映射文件。</param>
    /// <returns>文件元数据。</returns>
    internal static ConfigFileResponse ToResponse(ConfigFile file) =>
        new(file.Id, file.EnvironmentId, file.Name, file.DraftRevision, file.CurrentReleaseVersion, file.ConcurrencyStamp);
}
