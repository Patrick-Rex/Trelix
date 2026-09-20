using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence.Configurations;

public sealed class AdministratorMapping : IEntityTypeConfiguration<Administrator>
{
    public void Configure(EntityTypeBuilder<Administrator> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SecurityStamp).IsConcurrencyToken();
        builder.ToTable(table => table.HasCheckConstraint("CK_Administrators_Singleton", "Id = 1"));
    }
}

public sealed class AdministratorSessionMapping : IEntityTypeConfiguration<AdministratorSession>
{
    public void Configure(EntityTypeBuilder<AdministratorSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<Administrator>().WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ExpiresAt);
    }
}

public sealed class ApplicationTokenMapping : IEntityTypeConfiguration<ApplicationToken>
{
    public void Configure(EntityTypeBuilder<ApplicationToken> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.SecretHash).IsUnique();
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasMany(x => x.Scopes).WithOne().HasForeignKey(x => x.ApplicationTokenId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable(table => table.HasCheckConstraint("CK_ApplicationTokens_Expiry", "ExpiresAt > CreatedAt"));
    }
}

public sealed class TokenScopeMapping : IEntityTypeConfiguration<TokenScope>
{
    public void Configure(EntityTypeBuilder<TokenScope> builder)
    {
        builder.HasKey(x => new { x.ApplicationTokenId, x.EnvironmentId });
        builder.HasOne<ProjectEnvironment>().WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
