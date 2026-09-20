using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence;

public sealed class DatabaseInitializer(TrelixDbContext db, IConfiguration configuration, IPasswordHasher<Administrator> passwords)
{
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
