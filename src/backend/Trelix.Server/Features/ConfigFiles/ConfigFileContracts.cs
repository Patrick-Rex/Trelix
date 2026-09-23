using System.ComponentModel.DataAnnotations;

namespace Trelix.Server.Features.ConfigFiles;

/// <summary>创建空配置文件，之后独立保存草稿。</summary>
public sealed record CreateConfigFileRequest
{
    /// <summary>环境内唯一且区分大小写的文件名。</summary>
    [Required, MaxLength(256)]
    public required string Name { get; init; }
}

/// <summary>重命名文件，保持文件身份、草稿及历史不变。</summary>
public sealed record RenameConfigFileRequest
{
    /// <summary>新的文件名。</summary>
    [Required, MaxLength(256)]
    public required string Name { get; init; }
    /// <summary>读取文件时取得的并发基准。</summary>
    public required Guid ConcurrencyStamp { get; init; }
}

/// <summary>将 JSON 原文保存为新的草稿修订。</summary>
public sealed record SaveDraftRequest
{
    /// <summary>待校验和原样保存的 JSON 正文。</summary>
    [Required]
    public required string Json { get; init; }
    /// <summary>读取文件时取得的并发基准。</summary>
    public required Guid ConcurrencyStamp { get; init; }
}

/// <summary>不包含正文的配置文件元数据。</summary>
/// <param name="Id">文件稳定标识。</param>
/// <param name="EnvironmentId">所属环境标识。</param>
/// <param name="Name">文件名。</param>
/// <param name="DraftRevision">草稿修订；未保存时为 0。</param>
/// <param name="CurrentReleaseVersion">当前发布版本；未发布时为 null。</param>
/// <param name="ConcurrencyStamp">下一写操作的并发基准。</param>
public sealed record ConfigFileResponse(Guid Id, Guid EnvironmentId, string Name, long DraftRevision,
    long? CurrentReleaseVersion, Guid ConcurrencyStamp);

/// <summary>管理员可编辑的草稿快照及对应文件元数据。</summary>
/// <param name="File">与正文同时读取的文件状态。</param>
/// <param name="Json">JSON 原文；未保存时为 null。</param>
public sealed record DraftResponse(ConfigFileResponse File, string? Json);
