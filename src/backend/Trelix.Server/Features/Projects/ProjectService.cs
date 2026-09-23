using Microsoft.EntityFrameworkCore;
using Trelix.Server.Infrastructure;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Features.Projects;

/// <summary>管理项目与环境身份、层级及空资源删除。</summary>
/// <param name="db">当前请求的数据库上下文。</param>
public sealed class ProjectService(TrelixDbContext db)
{
    /// <summary>按稳定标识顺序分页读取项目。</summary>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>项目分页结果。</returns>
    public async Task<PageResponse<ProjectResponse>> ListProjectsAsync(int page, int pageSize, CancellationToken ct) =>
        new(await db.Projects.OrderBy(x => x.Key).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ProjectResponse(x.Id, x.Key, x.DisplayName, x.ConcurrencyStamp)).ToListAsync(ct), page, pageSize);

    /// <summary>读取指定项目元数据。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>项目元数据。</returns>
    public async Task<ProjectResponse> GetProjectAsync(Guid projectId, CancellationToken ct) =>
        await db.Projects.Where(x => x.Id == projectId)
            .Select(x => new ProjectResponse(x.Id, x.Key, x.DisplayName, x.ConcurrencyStamp)).SingleOrDefaultAsync(ct)
        ?? throw Missing();

    /// <summary>创建唯一业务标识的项目。</summary>
    /// <param name="request">项目身份与展示信息。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已创建项目及并发基准。</returns>
    public async Task<ProjectResponse> CreateProjectAsync(ResourceRequest request, CancellationToken ct)
    {
        var project = new Project { Key = request.Key, DisplayName = request.DisplayName };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return new(project.Id, project.Key, project.DisplayName, project.ConcurrencyStamp);
    }

    /// <summary>根据并发基准重命名项目，保留内部身份及关联授权。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="request">新名称与并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>更新后的项目元数据。</returns>
    public async Task<ProjectResponse> UpdateProjectAsync(Guid projectId, ResourceRequest request, CancellationToken ct)
    {
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == projectId, ct) ?? throw Missing();
        RequireStamp(request.ConcurrencyStamp, project.ConcurrencyStamp);
        project.Key = request.Key;
        project.DisplayName = request.DisplayName;
        db.Entry(project).Property(x => x.ConcurrencyStamp).IsModified = true;
        await db.SaveChangesAsync(ct);
        return new(project.Id, project.Key, project.DisplayName, project.ConcurrencyStamp);
    }

    /// <summary>删除无环境的项目，数据库外键保护并发新增环境的情况。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="stamp">调用方并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>删除任务。</returns>
    public async Task DeleteProjectAsync(Guid projectId, Guid stamp, CancellationToken ct)
    {
        var project = await db.Projects.SingleOrDefaultAsync(x => x.Id == projectId, ct) ?? throw Missing();
        RequireStamp(stamp, project.ConcurrencyStamp);
        if (await db.Environments.AnyAsync(x => x.ProjectId == projectId, ct))
            throw InUse();
        db.Projects.Remove(project);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>分页读取项目下的环境，拒绝不存在的父项目。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="page">页码。</param>
    /// <param name="pageSize">页大小。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>环境分页结果。</returns>
    public async Task<PageResponse<EnvironmentResponse>> ListEnvironmentsAsync(Guid projectId, int page, int pageSize, CancellationToken ct)
    {
        await GetProjectAsync(projectId, ct);
        return new(await db.Environments.Where(x => x.ProjectId == projectId).OrderBy(x => x.Key).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new EnvironmentResponse(x.Id, x.ProjectId, x.Key, x.DisplayName, x.ConcurrencyStamp)).ToListAsync(ct), page, pageSize);
    }

    /// <summary>读取属于指定项目的环境。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>环境元数据。</returns>
    public async Task<EnvironmentResponse> GetEnvironmentAsync(Guid projectId, Guid environmentId, CancellationToken ct) =>
        await db.Environments.Where(x => x.ProjectId == projectId && x.Id == environmentId)
            .Select(x => new EnvironmentResponse(x.Id, x.ProjectId, x.Key, x.DisplayName, x.ConcurrencyStamp)).SingleOrDefaultAsync(ct)
        ?? throw Missing();

    /// <summary>在现有项目中创建环境。</summary>
    /// <param name="projectId">所属项目标识。</param>
    /// <param name="request">环境身份与显示信息。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已创建环境。</returns>
    public async Task<EnvironmentResponse> CreateEnvironmentAsync(Guid projectId, ResourceRequest request, CancellationToken ct)
    {
        await GetProjectAsync(projectId, ct);
        var environment = new ProjectEnvironment { ProjectId = projectId, Key = request.Key, DisplayName = request.DisplayName };
        db.Environments.Add(environment);
        await db.SaveChangesAsync(ct);
        return new(environment.Id, projectId, environment.Key, environment.DisplayName, environment.ConcurrencyStamp);
    }

    /// <summary>重命名环境并保留其内部身份及令牌授权。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="request">新名称与并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>更新后的环境。</returns>
    public async Task<EnvironmentResponse> UpdateEnvironmentAsync(Guid projectId, Guid environmentId, ResourceRequest request, CancellationToken ct)
    {
        var environment = await db.Environments.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == environmentId, ct) ?? throw Missing();
        RequireStamp(request.ConcurrencyStamp, environment.ConcurrencyStamp);
        environment.Key = request.Key;
        environment.DisplayName = request.DisplayName;
        db.Entry(environment).Property(x => x.ConcurrencyStamp).IsModified = true;
        await db.SaveChangesAsync(ct);
        return new(environment.Id, projectId, environment.Key, environment.DisplayName, environment.ConcurrencyStamp);
    }

    /// <summary>删除无文件且无关联令牌授权的环境。</summary>
    /// <param name="projectId">项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="stamp">调用方并发基准。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>删除任务。</returns>
    public async Task DeleteEnvironmentAsync(Guid projectId, Guid environmentId, Guid stamp, CancellationToken ct)
    {
        var environment = await db.Environments.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.Id == environmentId, ct) ?? throw Missing();
        RequireStamp(stamp, environment.ConcurrencyStamp);
        if (await db.ConfigFiles.AnyAsync(x => x.EnvironmentId == environmentId, ct)
            || await db.TokenScopes.AnyAsync(x => x.EnvironmentId == environmentId, ct))
            throw InUse();
        db.Environments.Remove(environment);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>校验调用方提供了非空且仍有效的并发标记。</summary>
    /// <param name="expected">调用方基准。</param>
    /// <param name="actual">持久化标记。</param>
    public static void RequireStamp(Guid? expected, Guid actual)
    {
        if (expected is null || expected == Guid.Empty)
            throw new ApiOperationException(400, "invalid_request", "必须提供资源的并发基准。");
        if (expected != actual)
            throw new ApiOperationException(409, "concurrent_change", "资源已被其他操作修改，请刷新后重试。");
    }

    /// <summary>创建不暴露请求输入的资源缺失错误。</summary>
    /// <returns>资源不存在错误。</returns>
    private static ApiOperationException Missing() => new(404, "resource_not_found", "资源不存在或不属于指定父资源。");

    /// <summary>创建有关联资源时的删除冲突错误。</summary>
    /// <returns>资源仍被引用的冲突错误。</returns>
    private static ApiOperationException InUse() => new(409, "resource_in_use", "资源仍有下级资源或关联授权，无法删除。");
}
