namespace Trelix.Server.Persistence.Entities;

public sealed class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
}

public sealed class ProjectEnvironment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public required string Key { get; set; }
    public required string DisplayName { get; set; }
}

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

public sealed class Release
{
    public Guid ConfigFileId { get; set; }
    public long Version { get; set; }
    public required string Json { get; set; }
    public DateTimeOffset PublishedAt { get; set; }
    public long DraftRevision { get; set; }
    public long? SourceVersion { get; set; }
}
