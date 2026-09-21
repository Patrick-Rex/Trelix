using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Features.Authentication;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Tests;

/// <summary>使用真实 SQLite 验证关系约束、并发保护、事务与重启恢复。</summary>
public sealed class PersistenceTests
{
    /// <summary>创建两个含草稿、发布历史和当前发布指向的测试文件。</summary>
    /// <param name="app">当前集成测试宿主。</param>
    /// <param name="environmentId">目标环境标识。</param>
    /// <returns>两个测试文件的标识。</returns>
    private static async Task<(Guid FirstFile, Guid SecondFile)> SeedFilesAsync(ServerFactory app, Guid environmentId)
    {
        return await app.WithDbAsync(async db =>
        {
            var first = new ConfigFile { EnvironmentId = environmentId, Name = "first.json", DraftJson = "{\"value\":2}", DraftRevision = 2 };
            var second = new ConfigFile { EnvironmentId = environmentId, Name = "second.json", DraftJson = "{}", DraftRevision = 1 };
            db.AddRange(first, second);
            await db.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken);
            db.Releases.AddRange(
                new Release { ConfigFileId = first.Id, Version = 1, DraftRevision = 1, Json = "{\"value\":1}", PublishedAt = app.Clock.GetUtcNow() },
                new Release { ConfigFileId = second.Id, Version = 2, DraftRevision = 1, Json = "{}", PublishedAt = app.Clock.GetUtcNow() });
            await db.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken);
            first.CurrentReleaseVersion = 1;
            second.CurrentReleaseVersion = 2;
            await db.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken);
            return (first.Id, second.Id);
        });
    }

    /// <summary>验证 SQLite 拒绝重复业务标识、孤立或跨文件关联以及无效 JSON。</summary>
    /// <param name="scenario">本次验证的无效输入或数据库约束场景。</param>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Theory]
    [InlineData("project-key")]
    [InlineData("environment-key")]
    [InlineData("file-name")]
    [InlineData("release-version")]
    [InlineData("administrator-singleton")]
    [InlineData("orphan-environment")]
    [InlineData("orphan-file")]
    [InlineData("orphan-scope")]
    [InlineData("cross-file-current")]
    [InlineData("cross-file-source")]
    [InlineData("invalid-json")]
    public async Task SqliteEnforcesIdentityAndRelationshipConstraints(string scenario)
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        var resources = await app.SeedResourcesAsync();
        var files = await SeedFilesAsync(app, resources.FirstEnv.Id);
        await app.WithDbAsync(async db =>
        {
            switch (scenario)
            {
                case "project-key":
                    db.Projects.Add(new Project { Key = resources.First.Key, DisplayName = "Duplicate" });
                    break;
                case "environment-key":
                    db.Environments.Add(new ProjectEnvironment { ProjectId = resources.First.Id, Key = resources.FirstEnv.Key, DisplayName = "Duplicate" });
                    break;
                case "file-name":
                    db.ConfigFiles.Add(new ConfigFile { EnvironmentId = resources.FirstEnv.Id, Name = "first.json" });
                    break;
                case "release-version":
                    db.Releases.Add(new Release { ConfigFileId = files.FirstFile, Version = 1, DraftRevision = 1, Json = "{}", PublishedAt = app.Clock.GetUtcNow() });
                    break;
                case "administrator-singleton":
                    db.Administrators.Add(new Administrator { Id = 2, Username = "another", PasswordHash = "invalid-hash" });
                    break;
                case "orphan-environment":
                    db.Environments.Add(new ProjectEnvironment { ProjectId = Guid.NewGuid(), Key = "orphan", DisplayName = "Orphan" });
                    break;
                case "orphan-file":
                    db.ConfigFiles.Add(new ConfigFile { EnvironmentId = Guid.NewGuid(), Name = "orphan.json" });
                    break;
                case "orphan-scope":
                    db.TokenScopes.Add(new TokenScope { ApplicationTokenId = Guid.NewGuid(), EnvironmentId = resources.FirstEnv.Id });
                    break;
                case "cross-file-current":
                    (await db.ConfigFiles.SingleAsync(x => x.Id == files.FirstFile, cancellationToken: TestContext.Current.CancellationToken)).CurrentReleaseVersion = 2;
                    break;
                case "cross-file-source":
                    db.Releases.Add(new Release { ConfigFileId = files.FirstFile, Version = 3, SourceVersion = 2,
                        DraftRevision = 2, Json = "{}", PublishedAt = app.Clock.GetUtcNow() });
                    break;
                case "invalid-json":
                    (await db.ConfigFiles.SingleAsync(x => x.Id == files.FirstFile, cancellationToken: TestContext.Current.CancellationToken)).DraftJson = "not-json";
                    break;
            }

            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(19, Assert.IsType<SqliteException>(failure.InnerException).SqliteErrorCode);
            return true;
        });
    }

    /// <summary>验证 UTC 时间精度、时区归一化及大于 Int32 的版本比较排序。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task UtcTimeAndInt64VersionsAreComparedAndOrderedInSqlite()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        var resources = await app.SeedResourcesAsync();
        var files = await SeedFilesAsync(app, resources.FirstEnv.Id);
        const long largeVersion = 4_294_967_299;
        var later = new DateTimeOffset(2030, 1, 2, 0, 0, 0, TimeSpan.FromHours(8));
        var earlier = new DateTimeOffset(2030, 1, 1, 15, 0, 0, TimeSpan.Zero);
        await app.WithDbAsync(async db =>
        {
            db.Releases.Add(new Release { ConfigFileId = files.FirstFile, Version = largeVersion, SourceVersion = 1,
                DraftRevision = 2, Json = "{}", PublishedAt = later });
            db.ApplicationTokens.AddRange(
                new ApplicationToken { Name = "later", SecretHash = ApplicationTokenSecret.Hash(ApplicationTokenSecret.Create()), CreatedAt = earlier.AddHours(-1), ExpiresAt = later },
                new ApplicationToken { Name = "earlier", SecretHash = ApplicationTokenSecret.Hash(ApplicationTokenSecret.Create()), CreatedAt = earlier.AddHours(-1), ExpiresAt = earlier });
            await db.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken);
            return true;
        });
        await app.WithDbAsync(async db =>
        {
            var versions = await db.Releases.Where(x => x.ConfigFileId == files.FirstFile && x.Version > int.MaxValue).Select(x => x.Version).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal([largeVersion], versions);
            Assert.Equal(["earlier", "later"], await db.ApplicationTokens.OrderBy(x => x.ExpiresAt).Select(x => x.Name).ToArrayAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal("later", await db.ApplicationTokens.Where(x => x.ExpiresAt > earlier).Select(x => x.Name).SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
            var storedTime = await db.Releases.Where(x => x.ConfigFileId == files.FirstFile && x.Version == largeVersion).Select(x => x.PublishedAt).SingleAsync(cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(TimeSpan.Zero, storedTime.Offset);
            Assert.Equal(later.UtcTicks, storedTime.UtcTicks);
            return true;
        });
    }

    /// <summary>验证过期并发基准被拒绝且已发布历史不可修改。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task FileConcurrencyAndImmutableHistoryAreEnforced()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        var resources = await app.SeedResourcesAsync();
        var files = await SeedFilesAsync(app, resources.FirstEnv.Id);
        await using var scopeOne = app.Services.CreateAsyncScope();
        await using var scopeTwo = app.Services.CreateAsyncScope();
        var firstDb = scopeOne.ServiceProvider.GetRequiredService<TrelixDbContext>();
        var secondDb = scopeTwo.ServiceProvider.GetRequiredService<TrelixDbContext>();
        var first = await firstDb.ConfigFiles.SingleAsync(x => x.Id == files.FirstFile, cancellationToken: TestContext.Current.CancellationToken);
        var stale = await secondDb.ConfigFiles.SingleAsync(x => x.Id == files.FirstFile, cancellationToken: TestContext.Current.CancellationToken);
        first.DraftJson = "{\"value\":3}";
        first.DraftRevision++;
        await firstDb.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken);
        stale.DraftJson = "{\"value\":4}";
        stale.DraftRevision++;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken));
        var history = await firstDb.Releases.SingleAsync(x => x.ConfigFileId == files.FirstFile && x.Version == 1, cancellationToken: TestContext.Current.CancellationToken);
        history.Json = "{}";
        await Assert.ThrowsAsync<InvalidOperationException>(() => firstDb.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("{\"value\":1}", await app.WithDbAsync(db => db.Releases.Where(x => x.ConfigFileId == files.FirstFile).Select(x => x.Json).SingleAsync(cancellationToken: TestContext.Current.CancellationToken)));
    }

    /// <summary>注入 SQLite 写入故障，验证轮换事务同时回滚旧令牌撤销与新令牌创建。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task FailedRotationRollsBackRevocationAndReplacement()
    {
        using var data = new TestDataDirectory();
        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var resources = await app.SeedResourcesAsync();
        using var creation = await admin.PostAsJsonAsync("/api/admin/application-tokens", new CreateApplicationTokenRequest
        {
            Name = "consumer", ExpiresAt = app.Clock.GetUtcNow().AddHours(1),
            Scopes = [new TokenScopeRequest(resources.First.Id, resources.FirstEnv.Id)]
        }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
        var original = (await creation.Content.ReadFromJsonAsync<IssuedApplicationTokenResponse>(cancellationToken: TestContext.Current.CancellationToken))!;
        // A real SQLite trigger injects a persistence failure inside SaveChanges' transaction.
        await app.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectNewScopes BEFORE INSERT ON TokenScopes BEGIN SELECT RAISE(ABORT, 'injected storage failure'); END;", cancellationToken: TestContext.Current.CancellationToken));
        using var rotation = await admin.PostAsJsonAsync($"/api/admin/application-tokens/{original.Token.Id}/rotate", new { expiresAt = app.Clock.GetUtcNow().AddDays(1) }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.InternalServerError, rotation.StatusCode);
        await app.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.ApplicationTokens.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Null((await db.ApplicationTokens.SingleAsync(cancellationToken: TestContext.Current.CancellationToken)).RevokedAt);
            Assert.Equal(1, await db.TokenScopes.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
            return true;
        });
        using var consumer = app.NewClient(false);
        consumer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", original.Secret);
        using var stillValid = await consumer.GetAsync($"/__tests/application/{resources.First.Id}/{resources.FirstEnv.Id}", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, stillValid.StatusCode);
    }

    /// <summary>重建宿主后验证配置历史、管理员凭证、原会话及应用授权仍可用。</summary>
    /// <returns>表示测试场景执行完成的任务。</returns>
    [Fact]
    public async Task RestartKeepsConfigurationHistoryAdminSessionAndApplicationAuthorization()
    {
        using var data = new TestDataDirectory();
        string cookie, secret, password, username;
        Guid projectId, environmentId, fileId;
        await using (var initial = new ServerFactory(data))
        {
            username = initial.Username;
            password = initial.Password;
            using var admin = initial.NewClient();
            cookie = await initial.LoginAsync(admin);
            var resources = await initial.SeedResourcesAsync();
            projectId = resources.First.Id;
            environmentId = resources.FirstEnv.Id;
            fileId = (await SeedFilesAsync(initial, environmentId)).FirstFile;
            using var creation = await admin.PostAsJsonAsync("/api/admin/application-tokens", new CreateApplicationTokenRequest
            {
                Name = "consumer", ExpiresAt = initial.Clock.GetUtcNow().AddDays(1), Scopes = [new TokenScopeRequest(projectId, environmentId)]
            }, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, creation.StatusCode);
            secret = (await creation.Content.ReadFromJsonAsync<IssuedApplicationTokenResponse>(cancellationToken: TestContext.Current.CancellationToken))!.Secret;
        }

        await using var restarted = new ServerFactory(data) { Username = "replacement-ignored", Password = "" };
        using var retainedSession = restarted.NewClient(false);
        retainedSession.DefaultRequestHeaders.Add("Cookie", cookie);
        using var session = await retainedSession.GetAsync("/api/admin/auth/session", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
        using var loginClient = restarted.NewClient();
        await ServerFactory.RefreshCsrfAsync(loginClient);
        using var login = await loginClient.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest { Username = username, Password = password }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var consumer = restarted.NewClient(false);
        consumer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        using var allowed = await consumer.GetAsync($"/__tests/application/{projectId}/{environmentId}", cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        await restarted.WithDbAsync(async db =>
        {
            Assert.Equal(1, await db.Administrators.CountAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Equal(username, await db.Administrators.Select(x => x.Username).SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
            var file = await db.ConfigFiles.SingleAsync(x => x.Id == fileId, cancellationToken: TestContext.Current.CancellationToken);
            Assert.Equal("{\"value\":2}", file.DraftJson);
            Assert.Equal(2, file.DraftRevision);
            Assert.Equal(1, file.CurrentReleaseVersion);
            Assert.Equal("{\"value\":1}", await db.Releases.Where(x => x.ConfigFileId == file.Id && x.Version == file.CurrentReleaseVersion).Select(x => x.Json).SingleAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.Empty(await db.Database.GetPendingMigrationsAsync(cancellationToken: TestContext.Current.CancellationToken));
            Assert.False(db.Database.HasPendingModelChanges());
            Assert.Single(await db.Database.GetAppliedMigrationsAsync(cancellationToken: TestContext.Current.CancellationToken));
            return true;
        });
    }
}
