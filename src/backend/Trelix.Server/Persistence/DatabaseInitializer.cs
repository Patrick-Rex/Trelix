using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence;

/// <summary>在接受请求前应用数据库迁移，并按外部凭证初始化唯一管理员。</summary>
/// <param name="db">当前作用域的数据库上下文。</param>
/// <param name="configuration">外部注入的应用配置。</param>
/// <param name="passwords">管理员密码哈希生成与校验器。</param>
public sealed class DatabaseInitializer(TrelixDbContext db, IConfiguration configuration, IPasswordHasher<Administrator> passwords)
{
    /// <summary>应用迁移；仅在管理员不存在时校验初始化配置并保存密码哈希。</summary>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>表示初始化完成的任务。</returns>
    /// <exception cref="InvalidOperationException">首次初始化时管理员配置缺失或不符合格式约定。</exception>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (await db.Administrators.AnyAsync(cancellationToken))
            return;

        var username = configuration["Trelix:Administrator:Username"];
        var password = configuration["Trelix:Administrator:Password"];
        if (string.IsNullOrWhiteSpace(username) || username.Length > 128 || username != username.Trim()
            || string.IsNullOrWhiteSpace(password) || password.Length is < 12 or > 1024)
        {
            throw new InvalidOperationException("Configure Trelix:Administrator:Username (1–128 characters, no surrounding whitespace) and Trelix:Administrator:Password (12–1024 characters) before first startup.");
        }

        var administrator = new Administrator { Username = username, PasswordHash = "" };
        administrator.PasswordHash = passwords.HashPassword(administrator, password);
        db.Administrators.Add(administrator);
        await db.SaveChangesAsync(cancellationToken);
    }
}
