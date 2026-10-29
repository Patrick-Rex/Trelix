using Trelix.Server.Infrastructure.Persistence.Entities;

namespace Trelix.Server.Features.ConfigFiles;

/// <summary>将已物化的配置文件映射为管理 API 的响应。</summary>
internal static class ConfigFileMappings
{
    /// <summary>生成不包含正文的文件元数据。</summary>
    /// <param name="file">已物化的配置文件。</param>
    /// <returns>文件元数据及并发基准。</returns>
    internal static ConfigFileResponse ToResponse(this ConfigFile file) =>
        new(file.Id, file.EnvironmentId, file.Name, file.DraftRevision, file.CurrentReleaseVersion, file.ConcurrencyStamp);
}
