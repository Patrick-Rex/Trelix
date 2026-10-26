using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Distribution;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using static Trelix.Server.Tests.ConfigurationApiFixture;
using static Trelix.Server.Tests.ConfigurationPublishingTests;

namespace Trelix.Server.Tests;

/// <summary>验证复制所依赖的新文件与初始草稿原子创建、隔离及冲突行为。</summary>
public sealed class ConfigurationTransferTests
{
    /// <summary>携带正文的新文件从草稿修订一开始，手动发布前不能被应用读取。</summary>
    /// <returns>真实 SQLite 和 HTTP 验证任务。</returns>
    [Fact]
    public async Task InitialDraftIsPersistedWithoutPublishing()
    {
        await using var fixture = await CreateAsync();
        const string json = "{\n  \"Enabled\": true, \"Items\": [null, 2, \"中文\"]\n}";
        var file = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post, fixture.EnvironmentPath + "/files",
            new { name = "copied.json", json }, HttpStatusCode.Created);
        var path = fixture.EnvironmentPath + "/files/" + file.Id;
        var draft = await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, path);
        Assert.Equal(json, draft.Json);
        Assert.Equal(1, draft.File.DraftRevision);
        Assert.Null(draft.File.CurrentReleaseVersion);
        Assert.Equal(0, await fixture.App.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
        var token = await fixture.IssueAsync();
        using var consumer = fixture.Consumer(token.Secret);
        var readPath = fixture.ReadPath.Replace("app.json", "copied.json", StringComparison.Ordinal);
        await ExpectAsync(consumer, HttpMethod.Get, readPath, null, HttpStatusCode.NotFound);
        await SendAsync<PublicationResponse>(fixture.Admin, HttpMethod.Post, path + "/releases",
            new { file.DraftRevision, file.ConcurrencyStamp }, HttpStatusCode.Created);
        Assert.Equal(json, (await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, readPath)).Json);
    }

    /// <summary>省略或显式传入空初始正文均保留原有空文件创建契约。</summary>
    /// <param name="includeJson">是否显式传入值为 null 的 JSON 字段。</param>
    /// <returns>契约兼容性验证任务。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyCreationRemainsCompatible(bool includeJson)
    {
        await using var fixture = await CreateAsync();
        object body = includeJson ? new { name = "empty.json", json = (string?)null } : new { name = "empty.json" };
        var file = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post, fixture.EnvironmentPath + "/files", body, HttpStatusCode.Created);
        var draft = await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.EnvironmentPath + "/files/" + file.Id);
        Assert.Null(draft.Json);
        Assert.Equal(0, file.DraftRevision);
        Assert.Null(file.CurrentReleaseVersion);
    }

    /// <summary>无效初始正文被拒绝且不会留下空文件或部分草稿。</summary>
    /// <param name="json">无效配置正文。</param>
    /// <returns>校验与持久化边界验证任务。</returns>
    [Theory]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"A\":1,\"a\":2}")]
    [InlineData("{\"A:B\":1}")]
    [InlineData("{\"A\":")]
    public async Task InvalidInitialDraftDoesNotCreateFile(string json)
    {
        await using var fixture = await CreateAsync();
        await ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.EnvironmentPath + "/files", new { name = "invalid.json", json }, HttpStatusCode.BadRequest);
        Assert.Equal(1, await fixture.App.WithDbAsync(db => db.ConfigFiles.CountAsync(TestContext.Current.CancellationToken)));
        Assert.Null((await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath)).Json);
    }

    /// <summary>跨环境创建保留源草稿和发布，重复同名请求不会覆盖目标内容或历史。</summary>
    /// <returns>源目标隔离与重复创建验证任务。</returns>
    [Fact]
    public async Task CopyToAnotherEnvironmentNeverOverwritesSourceOrTarget()
    {
        await using var fixture = await CreateAsync();
        await fixture.SaveAsync("{\"Value\":1}");
        await fixture.PublishAsync();
        var source = await fixture.SaveAsync("{\"Value\":2}");
        var targetEnvironment = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Post,
            $"/api/admin/projects/{fixture.Project.Id}/environments", new { key = "target", displayName = "Target" }, HttpStatusCode.Created);
        var targetPath = $"/api/admin/projects/{fixture.Project.Id}/environments/{targetEnvironment.Id}/files";
        var target = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post, targetPath,
            new { name = fixture.File.Name, json = source.Json }, HttpStatusCode.Created);
        Assert.NotEqual(fixture.File.Id, target.Id);
        Assert.Null(target.CurrentReleaseVersion);
        var published = await SendAsync<PublicationResponse>(fixture.Admin, HttpMethod.Post, targetPath + $"/{target.Id}/releases",
            new { target.DraftRevision, target.ConcurrencyStamp }, HttpStatusCode.Created);
        await ExpectAsync(fixture.Admin, HttpMethod.Post, targetPath,
            new { name = fixture.File.Name, json = "{\"Value\":99}" }, HttpStatusCode.Conflict);
        var unchangedTarget = await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, targetPath + $"/{target.Id}");
        Assert.Equal(source.Json, unchangedTarget.Json);
        Assert.Equal(published.File, unchangedTarget.File);
        Assert.Equal(source, await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath));
        var originalRelease = await SendAsync<ReleaseDetailResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath + "/releases/1");
        Assert.Equal("{\"Value\":1}", originalRelease.Json);
        Assert.Equal(2, await fixture.App.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>并发同名复制最多创建一个完整草稿，其他请求返回冲突。</summary>
    /// <returns>数据库唯一约束与并发创建验证任务。</returns>
    [Fact]
    public async Task ConcurrentCopiesCreateOneCompleteDraft()
    {
        await using var fixture = await CreateAsync();
        const string json = "{\"Copied\":true}";
        var path = fixture.EnvironmentPath + "/files";
        var requests = Enumerable.Range(0, 2).Select(_ => fixture.Admin.PostAsJsonAsync(path,
            new { name = "concurrent.json", json }, TestContext.Current.CancellationToken));
        var responses = await Task.WhenAll(requests);
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            var file = await fixture.App.WithDbAsync(db => db.ConfigFiles.AsNoTracking().SingleAsync(
                item => item.Name == "concurrent.json", TestContext.Current.CancellationToken));
            Assert.Equal(json, file.DraftJson);
            Assert.Equal(1, file.DraftRevision);
            Assert.Null(file.CurrentReleaseVersion);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }
    }
}
