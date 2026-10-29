using Microsoft.EntityFrameworkCore;
using Trelix.Server.Common.Errors;
using Trelix.Server.Infrastructure.Persistence;
using Trelix.Server.Infrastructure.Persistence.Entities;

namespace Trelix.Server.Common.Configuration;

/// <summary>查询配置用例共同依赖的资源层级，不控制事务或保存变更。</summary>
internal static class ConfigurationResourceQueries
{
    /// <summary>确认环境属于指定项目，仅查询是否存在。</summary>
    /// <param name="db">当前用例的数据库上下文。</param>
    /// <param name="projectId">所属项目标识。</param>
    /// <param name="environmentId">环境标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>表示校验完成的任务。</returns>
    /// <exception cref="ApiOperationException">环境不存在或不属于指定项目。</exception>
    internal static async Task RequireEnvironmentAsync(this TrelixDbContext db, Guid projectId, Guid environmentId, CancellationToken ct)
    {
        if (!await db.Environments.AnyAsync(x => x.ProjectId == projectId && x.Id == environmentId, ct))
            throw ResourceErrors.Missing();
    }

    /// <summary>通过完整资源层级定位当前上下文跟踪的文件。</summary>
    /// <param name="db">当前用例的数据库上下文。</param>
    /// <param name="projectId">所属项目标识。</param>
    /// <param name="environmentId">所属环境标识。</param>
    /// <param name="fileId">文件标识。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>由当前上下文跟踪的文件。</returns>
    /// <exception cref="ApiOperationException">文件不存在或不属于指定的项目和环境。</exception>
    internal static async Task<ConfigFile> FindConfigFileAsync(this TrelixDbContext db,
        Guid projectId, Guid environmentId, Guid fileId, CancellationToken ct) =>
        await (from file in db.ConfigFiles
               join environment in db.Environments on file.EnvironmentId equals environment.Id
               where file.Id == fileId && environment.Id == environmentId && environment.ProjectId == projectId
               select file).SingleOrDefaultAsync(ct)
        ?? throw new ApiOperationException(404, "file_not_found", "配置文件不存在或不属于指定项目环境。");
}
