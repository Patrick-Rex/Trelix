using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Trelix.Server.Persistence;

// Migration generation does not initialize an administrator or touch the runtime database.
/// <summary>为 EF 迁移生成提供独立模型上下文，不初始化运行数据库或管理员。</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TrelixDbContext>
{
    /// <summary>创建使用内存 SQLite 配置的设计时上下文。</summary>
    /// <param name="args">EF 设计时工具参数；当前实现不使用。</param>
    /// <returns>供 EF 设计时工具使用的上下文。</returns>
    public TrelixDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<TrelixDbContext>().UseSqlite("Data Source=:memory:").Options);
}
