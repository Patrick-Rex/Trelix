using System.Net;
using System.Net.Http.Headers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Distribution;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using Trelix.Server.Persistence;
using static Trelix.Server.Tests.ConfigurationApiFixture;

namespace Trelix.Server.Tests;

/// <summary>验证 M2 有数据数据库升级与 M3 发布结果在宿主重建后仍可用。</summary>
public sealed class ConfigurationUpgradeTests
{
    /// <summary>从初始迁移数据库升级，保留回滚历史并初始化可写的资源并发标记。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task M2DatabaseUpgradesWithoutLosingPublishedHistory()
    {
        using var data = new TestDataDirectory();
        var projectId = Guid.NewGuid();
        var environmentId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var stamp = Guid.NewGuid();
        var connection = new SqliteConnectionStringBuilder { DataSource = Path.Combine(data.Path, "trelix.db"), ForeignKeys = true }.ToString();
        await using (var db = new TrelixDbContext(new DbContextOptionsBuilder<TrelixDbContext>().UseSqlite(connection).Options))
        {
            await db.GetService<IMigrator>().MigrateAsync("20260920163601_InitialStorage", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Projects (Id, Key, DisplayName) VALUES ({projectId}, 'legacy', 'Legacy');", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Environments (Id, ProjectId, Key, DisplayName) VALUES ({environmentId}, {projectId}, 'env', 'Environment');", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ConfigFiles (Id, EnvironmentId, Name, DraftJson, DraftRevision, ConcurrencyStamp) VALUES ({fileId}, {environmentId}, 'app.json', '{{}}', 1, {stamp});", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Releases (ConfigFileId, Version, Json, PublishedAt, DraftRevision) VALUES ({fileId}, 1, '{{}}', {DateTimeOffset.UtcNow.UtcTicks}, 1);", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO Releases (ConfigFileId, Version, Json, PublishedAt, DraftRevision, SourceVersion) VALUES ({fileId}, 2, '{{}}', {DateTimeOffset.UtcNow.UtcTicks}, 1, 1);", TestContext.Current.CancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE ConfigFiles SET CurrentReleaseVersion = 2 WHERE Id = {fileId};", TestContext.Current.CancellationToken);
        }

        await using var app = new ServerFactory(data);
        using var admin = app.NewClient();
        await app.LoginAsync(admin);
        var projectPath = $"/api/admin/projects/{projectId}";
        var project = await SendAsync<ProjectResponse>(admin, HttpMethod.Get, projectPath);
        Assert.NotEqual(Guid.Empty, project.ConcurrencyStamp);
        await SendAsync<ProjectResponse>(admin, HttpMethod.Put, projectPath, new { project.Key, displayName = "Renamed", project.ConcurrencyStamp });
        var environment = await SendAsync<EnvironmentResponse>(admin, HttpMethod.Get, projectPath + $"/environments/{environmentId}");
        Assert.NotEqual(Guid.Empty, environment.ConcurrencyStamp);
        var filePath = projectPath + $"/environments/{environmentId}/files/{fileId}";
        var file = await SendAsync<DraftResponse>(admin, HttpMethod.Get, filePath);
        Assert.Equal(stamp, file.File.ConcurrencyStamp);
        Assert.Equal(2, file.File.CurrentReleaseVersion);
        var history = await SendAsync<ReleaseDetailResponse>(admin, HttpMethod.Get, filePath + "/releases/2");
        Assert.Equal(1, history.Release.SourceVersion);
        Assert.Equal("{}", history.Json);
        using var delete = await admin.DeleteAsync(filePath + $"?concurrencyStamp={stamp}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, await app.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>通过 M3 API 发布后重建宿主，原应用令牌仍读取相同身份和正文。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task RestartServesTheSamePublishedSnapshot()
    {
        using var data = new TestDataDirectory();
        PublishedConfigResponse before;
        string secret;
        const string readPath = "/api/application/configuration?projectKey=project&environmentKey=env&fileName=app.json";
        await using (var initial = new ServerFactory(data))
        {
            using var admin = initial.NewClient();
            await initial.LoginAsync(admin);
            var project = await SendAsync<ProjectResponse>(admin, HttpMethod.Post, "/api/admin/projects", new { key = "project", displayName = "Project" }, HttpStatusCode.Created);
            var environment = await SendAsync<EnvironmentResponse>(admin, HttpMethod.Post, $"/api/admin/projects/{project.Id}/environments", new { key = "env", displayName = "Environment" }, HttpStatusCode.Created);
            var path = $"/api/admin/projects/{project.Id}/environments/{environment.Id}/files";
            var file = await SendAsync<ConfigFileResponse>(admin, HttpMethod.Post, path, new { name = "app.json" }, HttpStatusCode.Created);
            path += $"/{file.Id}";
            var draft = await SendAsync<DraftResponse>(admin, HttpMethod.Put, path + "/draft", new { json = "{\"published\":true}", file.ConcurrencyStamp });
            await SendAsync<PublicationResponse>(admin, HttpMethod.Post, path + "/releases", new { draft.File.DraftRevision, draft.File.ConcurrencyStamp }, HttpStatusCode.Created);
            var token = await SendAsync<IssuedApplicationTokenResponse>(admin, HttpMethod.Post, "/api/admin/application-tokens", new CreateApplicationTokenRequest
            {
                Name = "persistent", ExpiresAt = initial.Clock.GetUtcNow().AddHours(1), Scopes = [new(project.Id, environment.Id)]
            }, HttpStatusCode.Created);
            secret = token.Secret;
            using var consumer = initial.NewClient(false);
            consumer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            before = await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, readPath);
        }

        await using var restarted = new ServerFactory(data) { Password = "" };
        using var after = restarted.NewClient(false);
        after.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        Assert.Equal(before, await SendAsync<PublishedConfigResponse>(after, HttpMethod.Get, readPath));
    }
}
