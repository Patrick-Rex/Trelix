using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence.Configurations;

/// <summary>配置唯一内置管理员的主键、单例约束与安全戳并发检查。</summary>
public sealed class AdministratorMapping : IEntityTypeConfiguration<Administrator>
{
    /// <summary>配置唯一内置管理员的主键、单例约束与安全戳并发检查。</summary>
    /// <param name="builder">当前实体的 EF 映射构建器。</param>
    public void Configure(EntityTypeBuilder<Administrator> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.SecurityStamp).IsConcurrencyToken();
        builder.ToTable(table => table.HasCheckConstraint("CK_Administrators_Singleton", "Id = 1"));
    }
}

/// <summary>配置管理员会话关联、级联删除与到期时间索引。</summary>
public sealed class AdministratorSessionMapping : IEntityTypeConfiguration<AdministratorSession>
{
    /// <summary>配置管理员会话关联、级联删除与到期时间索引。</summary>
    /// <param name="builder">当前实体的 EF 映射构建器。</param>
    public void Configure(EntityTypeBuilder<AdministratorSession> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne<Administrator>().WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.ExpiresAt);
    }
}

/// <summary>配置令牌摘要唯一性、并发标记、授权关联与有效期约束。</summary>
public sealed class ApplicationTokenMapping : IEntityTypeConfiguration<ApplicationToken>
{
    /// <summary>配置令牌摘要唯一性、并发标记、授权关联与有效期约束。</summary>
    /// <param name="builder">当前实体的 EF 映射构建器。</param>
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

/// <summary>配置令牌与环境的联合主键及环境外键约束。</summary>
public sealed class TokenScopeMapping : IEntityTypeConfiguration<TokenScope>
{
    /// <summary>配置令牌与环境的联合主键及环境外键约束。</summary>
    /// <param name="builder">当前实体的 EF 映射构建器。</param>
    public void Configure(EntityTypeBuilder<TokenScope> builder)
    {
        builder.HasKey(x => new { x.ApplicationTokenId, x.EnvironmentId });
        builder.HasOne<ProjectEnvironment>().WithMany().HasForeignKey(x => x.EnvironmentId).OnDelete(DeleteBehavior.Restrict);
    }
}
