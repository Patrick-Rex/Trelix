using System.ComponentModel.DataAnnotations;
using Trelix.Server.Features.ConfigFiles;

namespace Trelix.Server.Features.Releases;

/// <summary>发布明确选中的当前草稿修订。</summary>
public sealed record PublishRequest
{
    /// <summary>需要发布的草稿修订号。</summary>
    [Range(1, long.MaxValue)]
    public required long DraftRevision { get; init; }
    /// <summary>调用方读取的文件并发基准。</summary>
    public required Guid ConcurrencyStamp { get; init; }
}

/// <summary>根据历史内容生成新发布版本。</summary>
public sealed record RollbackRequest
{
    /// <summary>本文件中选中的源历史版本号。</summary>
    [Range(1, long.MaxValue)]
    public required long SourceVersion { get; init; }
    /// <summary>调用方读取的文件并发基准。</summary>
    public required Guid ConcurrencyStamp { get; init; }
}

/// <summary>不可变发布记录的元数据，不包含配置正文。</summary>
/// <param name="ConfigFileId">文件稳定标识。</param>
/// <param name="Version">本文件内的发布版本号。</param>
/// <param name="DraftRevision">产生此内容的草稿修订号。</param>
/// <param name="SourceVersion">回滚来源；普通发布为 null。</param>
/// <param name="PublishedAt">UTC 发布时间。</param>
public sealed record ReleaseResponse(Guid ConfigFileId, long Version, long DraftRevision, long? SourceVersion, DateTimeOffset PublishedAt);

/// <summary>供管理历史查看使用的完整不可变快照。</summary>
/// <param name="Release">该正文所属的发布身份。</param>
/// <param name="Json">原始 JSON 正文。</param>
public sealed record ReleaseDetailResponse(ReleaseResponse Release, string Json);

/// <summary>成功发布后的文件基准及新版本元数据。</summary>
/// <param name="File">更新后的文件状态。</param>
/// <param name="Release">新发布记录。</param>
public sealed record PublicationResponse(ConfigFileResponse File, ReleaseResponse Release);
