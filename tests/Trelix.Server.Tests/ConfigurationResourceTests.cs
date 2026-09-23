using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Distribution;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using Trelix.Server.Infrastructure.Authentication;
using static Trelix.Server.Tests.ConfigurationApiFixture;
using static Trelix.Server.Tests.ConfigurationPublishingTests;

namespace Trelix.Server.Tests;

/// <summary>验证资源层级、身份重命名、原子删除以及应用真实读取权限。</summary>
public sealed class ConfigurationResourceTests
{
    /// <summary>重命名保持资源 ID 和授权；删除含回滚历史的文件后读取 404，同名重建使用新身份。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task RenameAndDeletePublishedFilePreserveIdentityRules()
    {
        await using var fixture = await CreateAsync();
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        fixture.File = (await SendAsync<PublicationResponse>(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/rollback",
            new { sourceVersion = 1, fixture.File.ConcurrencyStamp }, HttpStatusCode.Created)).File;
        var issued = await fixture.IssueAsync();
        using var consumer = fixture.Consumer(issued.Secret);
        var originalPath = fixture.ReadPath;
        var originalId = fixture.File.Id;
        var project = await SendAsync<ProjectResponse>(fixture.Admin, HttpMethod.Put, $"/api/admin/projects/{fixture.Project.Id}",
            new { key = "renamed-project", displayName = "Renamed", fixture.Project.ConcurrencyStamp });
        var environment = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Put, fixture.EnvironmentPath,
            new { key = "renamed-env", displayName = "Renamed", fixture.Environment.ConcurrencyStamp });
        fixture.File = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Put, fixture.FilePath,
            new { name = "renamed.json", fixture.File.ConcurrencyStamp });
        var renamedPath = $"/api/application/configuration?projectKey={project.Key}&environmentKey={environment.Key}&fileName={fixture.File.Name}";
        await ExpectAsync(consumer, HttpMethod.Get, originalPath, null, HttpStatusCode.NotFound);
        Assert.Equal(originalId, (await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, renamedPath)).ConfigFileId);
        using var deletion = await fixture.Admin.DeleteAsync(fixture.FilePath + $"?concurrencyStamp={fixture.File.ConcurrencyStamp}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        Assert.Equal(0, await fixture.App.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
        await ExpectAsync(consumer, HttpMethod.Get, renamedPath, null, HttpStatusCode.NotFound);
        var replacement = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post, fixture.EnvironmentPath + "/files",
            new { name = "renamed.json" }, HttpStatusCode.Created);
        Assert.NotEqual(originalId, replacement.Id);
        await ExpectAsync(consumer, HttpMethod.Get, renamedPath, null, HttpStatusCode.NotFound);
    }

    /// <summary>删除中途失败会恢复发布指向、历史和文件并发基准。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task FailedDeletionRollsBackEveryChange()
    {
        await using var fixture = await CreateAsync();
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        await fixture.App.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER RejectFileDelete BEFORE DELETE ON ConfigFiles BEGIN SELECT RAISE(ABORT, 'injected delete failure'); END;", TestContext.Current.CancellationToken));
        await ExpectAsync(fixture.Admin, HttpMethod.Delete, fixture.FilePath + $"?concurrencyStamp={fixture.File.ConcurrencyStamp}", null, HttpStatusCode.InternalServerError);
        Assert.Equal(fixture.File, (await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath)).File);
        Assert.Equal(1, await fixture.App.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>项目环境资源只能在无下级且无授权时删除，重命名和重复键受约束。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task HierarchyUniquenessPaginationAndResourceConcurrencyAreEnforced()
    {
        await using var fixture = await CreateAsync();
        var projectPath = $"/api/admin/projects/{fixture.Project.Id}";
        await ExpectAsync(fixture.Admin, HttpMethod.Post, "/api/admin/projects", new { key = fixture.Project.Key, displayName = "Duplicate" }, HttpStatusCode.Conflict);
        await ExpectAsync(fixture.Admin, HttpMethod.Post, projectPath + "/environments", new { key = fixture.Environment.Key, displayName = "Duplicate" }, HttpStatusCode.Conflict);
        await ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.EnvironmentPath + "/files", new { name = fixture.File.Name }, HttpStatusCode.Conflict);
        await ExpectAsync(fixture.Admin, HttpMethod.Delete, projectPath + $"?concurrencyStamp={fixture.Project.ConcurrencyStamp}", null, HttpStatusCode.Conflict);
        await ExpectAsync(fixture.Admin, HttpMethod.Delete, fixture.EnvironmentPath + $"?concurrencyStamp={fixture.Environment.ConcurrencyStamp}", null, HttpStatusCode.Conflict);
        var another = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Post, projectPath + "/environments",
            new { key = "other", displayName = "Other" }, HttpStatusCode.Created);
        var sameName = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post, projectPath + $"/environments/{another.Id}/files",
            new { name = fixture.File.Name }, HttpStatusCode.Created);
        Assert.NotEqual(fixture.File.Id, sameName.Id);
        await ExpectAsync(fixture.Admin, HttpMethod.Get, projectPath + $"/environments/{another.Id}/files/{fixture.File.Id}", null, HttpStatusCode.NotFound);
        var otherProject = await SendAsync<ProjectResponse>(fixture.Admin, HttpMethod.Post, "/api/admin/projects",
            new { key = "second", displayName = "Second" }, HttpStatusCode.Created);
        await ExpectAsync(fixture.Admin, HttpMethod.Get, $"/api/admin/projects/{otherProject.Id}/environments/{fixture.Environment.Id}", null, HttpStatusCode.NotFound);
        var page = await SendAsync<PageResponse<ProjectResponse>>(fixture.Admin, HttpMethod.Get, "/api/admin/projects?page=2&pageSize=1");
        Assert.Single(page.Items);
        Assert.Equal(otherProject.Id, page.Items[0].Id);
        var updated = await SendAsync<ProjectResponse>(fixture.Admin, HttpMethod.Put, projectPath,
            new { fixture.Project.Key, displayName = "Updated", fixture.Project.ConcurrencyStamp });
        Assert.NotEqual(fixture.Project.ConcurrencyStamp, updated.ConcurrencyStamp);
        await ExpectAsync(fixture.Admin, HttpMethod.Put, projectPath,
            new { fixture.Project.Key, displayName = "Stale", fixture.Project.ConcurrencyStamp }, HttpStatusCode.Conflict);
        var environment = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Put, fixture.EnvironmentPath,
            new { fixture.Environment.Key, displayName = "Updated", fixture.Environment.ConcurrencyStamp });
        await ExpectAsync(fixture.Admin, HttpMethod.Put, fixture.EnvironmentPath,
            new { fixture.Environment.Key, displayName = "Stale", fixture.Environment.ConcurrencyStamp }, HttpStatusCode.Conflict);
        await fixture.IssueAsync();
        using var removeFile = await fixture.Admin.DeleteAsync(fixture.FilePath + $"?concurrencyStamp={fixture.File.ConcurrencyStamp}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removeFile.StatusCode);
        await ExpectAsync(fixture.Admin, HttpMethod.Delete, fixture.EnvironmentPath + $"?concurrencyStamp={environment.ConcurrencyStamp}", null, HttpStatusCode.Conflict);
        using var removeProject = await fixture.Admin.DeleteAsync($"/api/admin/projects/{otherProject.Id}?concurrencyStamp={otherProject.ConcurrencyStamp}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removeProject.StatusCode);
        var empty = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Post, projectPath + "/environments", new { key = "empty", displayName = "Empty" }, HttpStatusCode.Created);
        using var removeEnvironment = await fixture.Admin.DeleteAsync(projectPath + $"/environments/{empty.Id}?concurrencyStamp={empty.ConcurrencyStamp}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, removeEnvironment.StatusCode);
    }

    /// <summary>真实读取入口拒绝无效生命周期或超出授权范围的应用令牌。</summary>
    /// <param name="scenario">凭证或授权场景。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("missing")]
    [InlineData("invalid")]
    [InlineData("expired")]
    [InlineData("revoked")]
    [InlineData("rotated")]
    [InlineData("cross-project")]
    [InlineData("cross-environment")]
    public async Task ApplicationReadEnforcesCredentialsAndScopes(string scenario)
    {
        await using var fixture = await CreateAsync();
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        var issued = await fixture.IssueAsync();
        var path = fixture.ReadPath;
        if (scenario == "expired")
            fixture.App.Clock.Advance(TimeSpan.FromHours(2));
        else if (scenario == "revoked")
        {
            using var response = await fixture.Admin.PostAsync($"/api/admin/application-tokens/{issued.Token.Id}/revoke", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
        else if (scenario == "rotated")
        {
            using var response = await fixture.Admin.PostAsJsonAsync($"/api/admin/application-tokens/{issued.Token.Id}/rotate",
                new { expiresAt = fixture.App.Clock.GetUtcNow().AddDays(1) }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        }
        else if (scenario.StartsWith("cross-", StringComparison.Ordinal))
        {
            var project = scenario == "cross-project"
                ? await SendAsync<ProjectResponse>(fixture.Admin, HttpMethod.Post, "/api/admin/projects", new { key = "other", displayName = "Other" }, HttpStatusCode.Created)
                : fixture.Project;
            var environment = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Post, $"/api/admin/projects/{project.Id}/environments",
                new { key = "other-env", displayName = "Other" }, HttpStatusCode.Created);
            path = $"/api/application/configuration?projectKey={project.Key}&environmentKey={environment.Key}&fileName=app.json";
        }
        using var consumer = fixture.Consumer(scenario == "missing" ? null : scenario == "invalid" ? "invalid-token" : issued.Secret);
        await ExpectAsync(consumer, HttpMethod.Get, path, null,
            scenario.StartsWith("cross-", StringComparison.Ordinal) ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized);
    }

    /// <summary>管理 Cookie 不能读取应用配置，应用令牌不能读取草稿或写入管理接口，写操作要求防伪造。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task ManagementAndApplicationEndpointsRemainSeparated()
    {
        await using var fixture = await CreateAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        await ExpectAsync(fixture.Admin, HttpMethod.Get, fixture.ReadPath, null, HttpStatusCode.Unauthorized);
        await ExpectAsync(consumer, HttpMethod.Get, fixture.FilePath, null, HttpStatusCode.Unauthorized);
        await ExpectAsync(consumer, HttpMethod.Put, fixture.FilePath + "/draft", new { json = "{}", fixture.File.ConcurrencyStamp }, HttpStatusCode.Unauthorized);
        fixture.Admin.DefaultRequestHeaders.Remove(AuthenticationConstants.CsrfHeader);
        await ExpectAsync(fixture.Admin, HttpMethod.Put, fixture.FilePath + "/draft", new { json = "{}", fixture.File.ConcurrencyStamp }, HttpStatusCode.BadRequest);
        await ExpectAsync(fixture.Admin, HttpMethod.Delete, fixture.FilePath + $"?concurrencyStamp={fixture.File.ConcurrencyStamp}", null, HttpStatusCode.BadRequest);
    }
}
