using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Trelix.Server.Persistence;

// Migration generation does not initialize an administrator or touch the runtime database.
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TrelixDbContext>
{
    public TrelixDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<TrelixDbContext>().UseSqlite("Data Source=:memory:").Options);
}
