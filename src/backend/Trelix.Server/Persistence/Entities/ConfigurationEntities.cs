namespace Trelix.Server.Persistence.Entities;

/// <summary>配置资源的顶层项目，使用唯一业务标识区分。</summary>
public sealed class Project
{
    /// <summary>项目的持久化标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>项目的唯一业务标识。</summary>
    public required string Key { get; set; }
    /// <summary>供管理界面展示的项目名称。</summary>
    public required string DisplayName { get; set; }
}

/// <summary>项目内的配置环境，其业务标识在所属项目内唯一。</summary>
public sealed class ProjectEnvironment
{
    /// <summary>环境的持久化标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>环境所属项目的标识。</summary>
    public Guid ProjectId { get; set; }
    /// <summary>在所属项目内唯一的环境业务标识。</summary>
    public required string Key { get; set; }
    /// <summary>供管理界面展示的环境名称。</summary>
    public required string DisplayName { get; set; }
}

/// <summary>环境中的配置文件，分别维护可修改草稿与当前发布版本指向。</summary>
public sealed class ConfigFile
{
    /// <summary>配置文件的持久化标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>文件所属环境的标识。</summary>
    public Guid EnvironmentId { get; set; }
    /// <summary>在所属环境内唯一的文件名。</summary>
    public required string Name { get; set; }
    /// <summary>当前草稿的 JSON 正文；尚无草稿时为 null。</summary>
    public string? DraftJson { get; set; }
    /// <summary>草稿修订号；尚无草稿时为 0。</summary>
    public long DraftRevision { get; set; }
    /// <summary>本文件当前发布版本号；尚未发布时为 null。</summary>
    public long? CurrentReleaseVersion { get; set; }
    /// <summary>修改文件时刷新的乐观并发标记。</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}

/// <summary>文件的不可变发布快照，记录草稿修订及可选回滚来源版本。</summary>
public sealed class Release
{
    /// <summary>该发布版本所属配置文件的标识。</summary>
    public Guid ConfigFileId { get; set; }
    /// <summary>在本文件内唯一、从正整数开始的发布版本号。</summary>
    public long Version { get; set; }
    /// <summary>发布时保存的不可变 JSON 正文快照。</summary>
    public required string Json { get; set; }
    /// <summary>发布发生的时间，持久化时归一化为 UTC。</summary>
    public DateTimeOffset PublishedAt { get; set; }
    /// <summary>本次发布关联的草稿修订号。</summary>
    public long DraftRevision { get; set; }
    /// <summary>回滚所依据的本文件历史版本号；普通发布时为 null。</summary>
    public long? SourceVersion { get; set; }
}
