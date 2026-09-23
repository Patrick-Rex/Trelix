using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence;

/// <summary>维护配置、发布历史及认证数据，并在保存前保护历史和刷新并发标记。</summary>
/// <param name="options">数据库上下文配置。</param>
public sealed class TrelixDbContext(DbContextOptions<TrelixDbContext> options) : DbContext(options)
{
    /// <summary>配置项目及其业务标识。</summary>
    public DbSet<Project> Projects => Set<Project>();
    /// <summary>项目下的配置环境。</summary>
    public DbSet<ProjectEnvironment> Environments => Set<ProjectEnvironment>();
    /// <summary>包含草稿及当前发布指向的配置文件。</summary>
    public DbSet<ConfigFile> ConfigFiles => Set<ConfigFile>();
    /// <summary>按文件和版本标识的不可变发布历史。</summary>
    public DbSet<Release> Releases => Set<Release>();
    /// <summary>唯一内置管理员的凭证与安全戳。</summary>
    public DbSet<Administrator> Administrators => Set<Administrator>();
    /// <summary>用于逐请求验证和退出撤销的管理员会话。</summary>
    public DbSet<AdministratorSession> AdministratorSessions => Set<AdministratorSession>();
    /// <summary>应用只读令牌摘要及生命周期信息。</summary>
    public DbSet<ApplicationToken> ApplicationTokens => Set<ApplicationToken>();
    /// <summary>应用令牌与获授权环境之间的关联。</summary>
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

        foreach (var entry in ChangeTracker.Entries<Project>())
            if (entry.State == EntityState.Modified)
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();

        foreach (var entry in ChangeTracker.Entries<ProjectEnvironment>())
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
