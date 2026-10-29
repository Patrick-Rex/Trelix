using Microsoft.EntityFrameworkCore;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Infrastructure.Persistence.Entities;

namespace Trelix.Server.Tests;

/// <summary>验证真实 SQLite 上各资源的并发标记与管理员安全戳边界。</summary>
public sealed class ConcurrencyStampTests
{
    /// <summary>验证同步与异步保存只刷新已修改资源的标记，并保留新增和未修改资源及管理员安全戳。</summary>
    /// <param name="synchronous">是否使用同步保存入口。</param>
    /// <returns>表示验证完成的任务。</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveRefreshesOnlyModifiedConcurrencyResources(bool synchronous)
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        var resources = await app.SeedResourcesAsync();
        await app.WithDbAsync(async db =>
        {
            var ct = TestContext.Current.CancellationToken;
            var project = await db.Projects.SingleAsync(x => x.Id == resources.First.Id, ct);
            var unchanged = await db.Projects.SingleAsync(x => x.Id == resources.Second.Id, ct);
            var environment = await db.Environments.SingleAsync(x => x.Id == resources.FirstEnv.Id, ct);
            var admin = await db.Administrators.SingleAsync(ct);
            var file = new ConfigFile { EnvironmentId = environment.Id, Name = "test.json" };
            var token = new ApplicationToken
            {
                Name = "test", SecretHash = ApplicationTokenSecret.Hash(ApplicationTokenSecret.Create()),
                CreatedAt = app.Clock.GetUtcNow(), ExpiresAt = app.Clock.GetUtcNow().AddDays(1)
            };
            db.AddRange(file, token);
            var newFileStamp = file.ConcurrencyStamp;
            var newTokenStamp = token.ConcurrencyStamp;
            await db.SaveChangesAsync(ct);
            Assert.Equal(newFileStamp, file.ConcurrencyStamp);
            Assert.Equal(newTokenStamp, token.ConcurrencyStamp);

            var oldProject = project.ConcurrencyStamp;
            var oldEnvironment = environment.ConcurrencyStamp;
            var oldUnchanged = unchanged.ConcurrencyStamp;
            var oldSecurity = admin.SecurityStamp;
            project.DisplayName = "Changed project";
            environment.DisplayName = "Changed environment";
            file.Name = "renamed.json";
            token.Name = "Changed token";
            admin.Username = "modified-admin";
            if (synchronous)
                db.SaveChanges();
            else
                await db.SaveChangesAsync(ct);

            Assert.NotEqual(oldProject, project.ConcurrencyStamp);
            Assert.NotEqual(oldEnvironment, environment.ConcurrencyStamp);
            Assert.NotEqual(newFileStamp, file.ConcurrencyStamp);
            Assert.NotEqual(newTokenStamp, token.ConcurrencyStamp);
            Assert.Equal(oldUnchanged, unchanged.ConcurrencyStamp);
            Assert.Equal(oldSecurity, admin.SecurityStamp);

            var beforeSameValue = file.ConcurrencyStamp;
            db.Entry(file).Property(x => x.ConcurrencyStamp).IsModified = true;
            if (synchronous)
                db.SaveChanges();
            else
                await db.SaveChangesAsync(ct);
            Assert.NotEqual(beforeSameValue, file.ConcurrencyStamp);
            var persistedProject = project.ConcurrencyStamp;
            var persistedEnvironment = environment.ConcurrencyStamp;
            var persistedFile = file.ConcurrencyStamp;
            var persistedToken = token.ConcurrencyStamp;
            var fileId = file.Id;
            var tokenId = token.Id;
            db.ChangeTracker.Clear();
            Assert.Equal(persistedProject, (await db.Projects.SingleAsync(x => x.Id == resources.First.Id, ct)).ConcurrencyStamp);
            Assert.Equal(persistedEnvironment, (await db.Environments.SingleAsync(x => x.Id == resources.FirstEnv.Id, ct)).ConcurrencyStamp);
            Assert.Equal(persistedFile, (await db.ConfigFiles.SingleAsync(x => x.Id == fileId, ct)).ConcurrencyStamp);
            Assert.Equal(persistedToken, (await db.ApplicationTokens.SingleAsync(x => x.Id == tokenId, ct)).ConcurrencyStamp);
            Assert.Equal(oldSecurity, (await db.Administrators.SingleAsync(ct)).SecurityStamp);
            return true;
        });
    }
}
