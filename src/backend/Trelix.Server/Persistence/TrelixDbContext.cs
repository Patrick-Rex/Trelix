using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence;

/// <summary>维护配置、发布历史及认证数据，并在保存前保护历史和刷新并发标记。</summary>
/// <param name="options">数据库上下文配置。</param>
public sealed class TrelixDbContext(DbContextOptions<TrelixDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectEnvironment> Environments => Set<ProjectEnvironment>();
    public DbSet<ConfigFile> ConfigFiles => Set<ConfigFile>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<AdministratorSession> AdministratorSessions => Set<AdministratorSession>();
    public DbSet<ApplicationToken> ApplicationTokens => Set<ApplicationToken>();
    public DbSet<TokenScope> TokenScopes => Set<TokenScope>();

    /// <summary>将时间统一映射为可在 SQLite 中比较和排序的 UTC ticks。</summary>
    /// <param name="configurationBuilder">EF 属性约定构建器。</param>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    /// <summary>加载 Server 程序集中的实体映射与数据库约束。</summary>
    /// <param name="modelBuilder">EF 实体模型构建器。</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrelixDbContext).Assembly);
    }

    /// <summary>拒绝修改已有发布记录，并为修改的配置文件和令牌刷新并发标记。</summary>
    /// <exception cref="InvalidOperationException">已有发布记录被标记为修改。</exception>
    private void PrepareChanges()
    {
        foreach (var entry in ChangeTracker.Entries<Release>())
        {
            if (entry.State == EntityState.Modified)
                throw new InvalidOperationException("Published releases are immutable.");
        }

        foreach (var entry in ChangeTracker.Entries<ConfigFile>())
            if (entry.State == EntityState.Modified)
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();

        foreach (var entry in ChangeTracker.Entries<ApplicationToken>())
            if (entry.State == EntityState.Modified)
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();
    }

    /// <summary>应用历史保护与并发标记规则后同步保存变更。</summary>
    /// <param name="acceptAllChangesOnSuccess">保存成功后是否接受跟踪状态中的全部变更。</param>
    /// <returns>写入数据库的状态条目数。</returns>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>应用历史保护与并发标记规则后异步保存变更。</summary>
    /// <param name="acceptAllChangesOnSuccess">保存成功后是否接受跟踪状态中的全部变更。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>写入数据库的状态条目数。</returns>
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
