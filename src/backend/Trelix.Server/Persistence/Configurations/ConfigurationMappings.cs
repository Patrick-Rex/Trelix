using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence.Configurations;

public sealed class ProjectMapping : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
    }
}

public sealed class EnvironmentMapping : IEntityTypeConfiguration<ProjectEnvironment>
{
    public void Configure(EntityTypeBuilder<ProjectEnvironment> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.ProjectId, x.Key }).IsUnique();
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
    }
}

public sealed class ConfigFileMapping : IEntityTypeConfiguration<ConfigFile>
{
    public void Configure(EntityTypeBuilder<ConfigFile> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.EnvironmentId, x.Name }).IsUnique();
        builder.HasOne<ProjectEnvironment>().WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Release>().WithMany()
            .HasForeignKey(x => new { x.Id, x.CurrentReleaseVersion })
            .HasPrincipalKey(x => new { x.ConfigFileId, x.Version }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Name).HasMaxLength(256);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ConfigFiles_Draft", "(DraftJson IS NULL AND DraftRevision = 0) OR (DraftJson IS NOT NULL AND json_valid(DraftJson) AND DraftRevision > 0)");
        });
    }
}

public sealed class ReleaseMapping : IEntityTypeConfiguration<Release>
{
    public void Configure(EntityTypeBuilder<Release> builder)
    {
        builder.HasKey(x => new { x.ConfigFileId, x.Version });
        builder.HasOne<ConfigFile>().WithMany().HasForeignKey(x => x.ConfigFileId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Release>().WithMany().HasForeignKey(x => new { x.ConfigFileId, x.SourceVersion })
            .HasPrincipalKey(x => new { x.ConfigFileId, x.Version }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.PublishedAt);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_Releases_Version", "Version > 0 AND DraftRevision > 0");
            table.HasCheckConstraint("CK_Releases_Json", "json_valid(Json)");
            table.HasCheckConstraint("CK_Releases_Source", "SourceVersion IS NULL OR SourceVersion < Version");
        });
    }
}
