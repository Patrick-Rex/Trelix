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
