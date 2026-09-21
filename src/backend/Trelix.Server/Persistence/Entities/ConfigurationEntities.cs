namespace Trelix.Server.Persistence.Entities;

/// <summary>配置资源的顶层项目，使用唯一业务标识区分。</summary>
public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
}

/// <summary>项目内的配置环境，其业务标识在所属项目内唯一。</summary>
public sealed class ProjectEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
}

/// <summary>环境中的配置文件，分别维护可修改草稿与当前发布版本指向。</summary>
public sealed class ConfigFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid EnvironmentId { get; set; }
    public required string Name { get; set; }
    public string? DraftJson { get; set; }
    public long DraftRevision { get; set; }
    public long? CurrentReleaseVersion { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}

/// <summary>文件的不可变发布快照，记录草稿修订及可选回滚来源版本。</summary>
public sealed class Release
{
    public Guid ConfigFileId { get; set; }
    public long Version { get; set; }
    public required string Json { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public long DraftRevision { get; set; }
    public long? SourceVersion { get; set; }
}
