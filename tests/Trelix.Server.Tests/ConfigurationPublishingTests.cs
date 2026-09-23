using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Distribution;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using Trelix.Server.Infrastructure.Authentication;
using static Trelix.Server.Tests.ConfigurationApiFixture;

namespace Trelix.Server.Tests;

/// <summary>从真实管理与应用入口验证草稿隔离、发布回滚、并发及 SQLite 事务。</summary>
public sealed class ConfigurationPublishingTests
{
    /// <summary>完整验证草稿、发布、历史与回滚新版本，原草稿及历史保持不变。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task DraftPublishAndRollbackKeepSnapshotsIsolated()
    {
        await using var fixture = await CreateAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        await ExpectAsync(consumer, HttpMethod.Get, fixture.ReadPath, null, HttpStatusCode.NotFound);
        const string first = "{\n  \"Connection\": {\"Enabled\": true}, \"Array\": [null, 1, \"值\"]\n}";
        await fixture.SaveAsync(first);
        await ExpectAsync(consumer, HttpMethod.Get, fixture.ReadPath, null, HttpStatusCode.NotFound);
        var published = await fixture.PublishAsync();
        Assert.Equal(1, published.Release.Version);
        Assert.Equal(first, (await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, fixture.ReadPath)).Json);
        await fixture.SaveAsync("{\"value\":2}");
        Assert.Equal(first, (await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, fixture.ReadPath)).Json);
        await fixture.PublishAsync();
        var saved = await fixture.SaveAsync("{\"unpublished\":3}");
        var rollback = await SendAsync<PublicationResponse>(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/rollback",
            new { sourceVersion = 1, fixture.File.ConcurrencyStamp }, HttpStatusCode.Created);
        Assert.Equal(3, rollback.Release.Version);
        Assert.Equal(1, rollback.Release.SourceVersion);
        Assert.Equal(1, rollback.Release.DraftRevision);
        Assert.Equal(saved.File.DraftRevision, rollback.File.DraftRevision);
        var draft = await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath);
        Assert.Equal(saved.Json, draft.Json);
        Assert.NotEqual(saved.File.ConcurrencyStamp, draft.File.ConcurrencyStamp);
        var current = await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, fixture.ReadPath);
        Assert.Equal(first, current.Json);
        Assert.Equal(rollback.Release.Version, current.Version);
        Assert.Equal(rollback.Release.PublishedAt, current.PublishedAt);
        var history = await SendAsync<PageResponse<ReleaseResponse>>(fixture.Admin, HttpMethod.Get, fixture.FilePath + "/releases?pageSize=2");
        Assert.Equal([3L, 2L], history.Items.Select(x => x.Version));
        var original = await SendAsync<ReleaseDetailResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath + "/releases/1");
        Assert.Equal(first, original.Json);
        Assert.Equal(published.Release, original.Release);
    }

    /// <summary>拒绝不兼容配置，且不改变草稿或泄露正文。</summary>
    /// <param name="scenario">JSON 无效场景。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("syntax")]
    [InlineData("root-array")]
    [InlineData("root-null")]
    [InlineData("duplicate")]
    [InlineData("nested-duplicate")]
    [InlineData("colon")]
    [InlineData("comment")]
    [InlineData("trailing-comma")]
    [InlineData("depth")]
    [InlineData("size")]
    [InlineData("unicode")]
    [InlineData("unicode-value")]
    public async Task InvalidJsonDoesNotChangeDraft(string scenario)
    {
        await using var fixture = await CreateAsync();
        var json = scenario switch
        {
            "syntax" => "{sensitive-config-body",
            "root-array" => "[]",
            "root-null" => "null",
            "duplicate" => "{\"Key\":1,\"key\":2}",
            "nested-duplicate" => "{\"list\":[{\"A\":1,\"a\":2}]}",
            "colon" => "{\"Parent:Child\":1}",
            "comment" => "{/* comment */}",
            "trailing-comma" => "{\"a\":1,}",
            "depth" => string.Concat(Enumerable.Repeat("{\"a\":", 65)) + "1" + new string('}', 65),
            "unicode" => "{\"\\uD800\":1}",
            "unicode-value" => "{\"value\":\"\\uD800\"}",
            _ => "{\"a\":\"" + new string('中', ConfigJson.MaxBytes / 3) + "\"}"
        };
        await ExpectAsync(fixture.Admin, HttpMethod.Put, fixture.FilePath + "/draft", new { json, fixture.File.ConcurrencyStamp }, HttpStatusCode.BadRequest);
        var draft = await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath);
        Assert.Null(draft.Json);
        Assert.Equal(fixture.File, draft.File);
        Assert.DoesNotContain(fixture.App.Logs.Entries, x => x.Contains("sensitive-config-body", StringComparison.Ordinal));
    }

    /// <summary>最大正文和最大深度的合法边界可保存。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task JsonLimitsAcceptExactBoundary()
    {
        await using var fixture = await CreateAsync();
        var json = "{\"a\":\"" + new string('a', ConfigJson.MaxBytes - 8) + "\"}";
        Assert.Equal(ConfigJson.MaxBytes, Encoding.UTF8.GetByteCount(json));
        Assert.Equal(json, (await fixture.SaveAsync(json)).Json);
        var deep = string.Concat(Enumerable.Repeat("{\"a\":", 64)) + "1" + new string('}', 64);
        Assert.Equal(deep, (await fixture.SaveAsync(deep)).Json);
    }

    /// <summary>所有改变文件的操作拒绝过期并发基准。</summary>
    /// <param name="operation">文件写操作。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("save")]
    [InlineData("publish")]
    [InlineData("rollback")]
    [InlineData("rename")]
    [InlineData("delete")]
    public async Task StaleFileWritesAreRejected(string operation)
    {
        await using var fixture = await CreateAsync();
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        var stale = fixture.File;
        await fixture.SaveAsync("{\"new\":true}");
        var (method, suffix, body) = Mutation(operation, stale);
        await ExpectAsync(fixture.Admin, method, fixture.FilePath + suffix, body, HttpStatusCode.Conflict);
        var draft = await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath);
        Assert.Equal(fixture.File, draft.File);
        Assert.Equal("{\"new\":true}", draft.Json);
        Assert.Equal(1, await fixture.App.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
    }

    /// <summary>两个实际 HTTP 写请求使用同一基准时仅有一个成功。</summary>
    /// <param name="operation">要并发执行的操作。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("save")]
    [InlineData("publish")]
    [InlineData("rollback")]
    public async Task ConcurrentWritesAllowOnlyOneWinner(string operation)
    {
        await using var fixture = await CreateAsync();
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        var (method, suffix, body) = Mutation(operation, fixture.File);
        using var first = new HttpRequestMessage(method, fixture.FilePath + suffix) { Content = JsonContent.Create(body) };
        using var second = new HttpRequestMessage(method, fixture.FilePath + suffix) { Content = JsonContent.Create(body) };
        var responses = await Task.WhenAll(fixture.Admin.SendAsync(first, TestContext.Current.CancellationToken),
            fixture.Admin.SendAsync(second, TestContext.Current.CancellationToken));
        try
        {
            Assert.Single(responses, x => x.IsSuccessStatusCode);
            Assert.Single(responses, x => x.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(operation == "save" ? 1 : 2,
                await fixture.App.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
        }
        finally
        {
            foreach (var response in responses)
                response.Dispose();
        }
    }

    /// <summary>发布事务中的历史插入或当前指向更新失败时全部回滚。</summary>
    /// <param name="stage">注入故障的位置。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("insert")]
    [InlineData("pointer")]
    public async Task FailedPublicationLeavesNoPartialState(string stage)
    {
        await using var fixture = await CreateAsync();
        await fixture.SaveAsync("{\"published\":1}");
        await fixture.PublishAsync();
        await fixture.SaveAsync("{\"sensitive-config-body\":2}");
        var before = fixture.File;
        var sql = stage == "insert"
            ? "CREATE TRIGGER RejectPublish BEFORE INSERT ON Releases BEGIN SELECT RAISE(ABORT, 'injected release failure'); END;"
            : "CREATE TRIGGER RejectPublish BEFORE UPDATE OF CurrentReleaseVersion ON ConfigFiles WHEN NEW.CurrentReleaseVersion <> OLD.CurrentReleaseVersion BEGIN SELECT RAISE(ABORT, 'injected pointer failure'); END;";
        await fixture.App.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken));
        await ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/releases",
            new { before.DraftRevision, before.ConcurrencyStamp }, HttpStatusCode.InternalServerError);
        var after = await SendAsync<DraftResponse>(fixture.Admin, HttpMethod.Get, fixture.FilePath);
        Assert.Equal(before, after.File);
        Assert.Equal(1, await fixture.App.WithDbAsync(db => db.Releases.CountAsync(TestContext.Current.CancellationToken)));
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        Assert.Equal("{\"published\":1}", (await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, fixture.ReadPath)).Json);
        Assert.DoesNotContain(fixture.App.Logs.Entries, x => x.Contains("sensitive-config-body", StringComparison.Ordinal));
    }

    /// <summary>拒绝不存在草稿、错修订、跨文件历史及错误请求绑定。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task MissingDraftRevisionAndHistoryReturnSafeProblems()
    {
        await using var fixture = await CreateAsync();
        await ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/releases",
            new { draftRevision = 1, fixture.File.ConcurrencyStamp }, HttpStatusCode.Conflict);
        await fixture.SaveAsync("{}");
        await ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/releases",
            new { draftRevision = 2, fixture.File.ConcurrencyStamp }, HttpStatusCode.Conflict);
        await ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/rollback",
            new { sourceVersion = 1, fixture.File.ConcurrencyStamp }, HttpStatusCode.NotFound);
        await ExpectAsync(fixture.Admin, HttpMethod.Put, fixture.FilePath + "/draft", new { json = "{}" }, HttpStatusCode.BadRequest);
        await ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/releases", new { draftRevision = 0, fixture.File.ConcurrencyStamp }, HttpStatusCode.BadRequest);
        await ExpectAsync(fixture.Admin, HttpMethod.Get, fixture.EnvironmentPath + "/files?pageSize=101", null, HttpStatusCode.BadRequest);
        using var invalid = new HttpRequestMessage(HttpMethod.Put, fixture.FilePath + "/draft") { Content = new StringContent("{sensitive-request-body", Encoding.UTF8, "application/json") };
        using var response = await fixture.Admin.SendAsync(invalid, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("sensitive-request-body", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(fixture.App.Logs.Entries, x => x.Contains("sensitive-request-body", StringComparison.Ordinal));
    }

    /// <summary>构造指定文件操作及其并发基准。</summary>
    /// <param name="operation">文件写操作名称。</param>
    /// <param name="file">请求使用的文件基准。</param>
    /// <returns>方法、路径后缀和请求正文。</returns>
    private static (HttpMethod Method, string Suffix, object? Body) Mutation(string operation, ConfigFileResponse file) => operation switch
    {
        "save" => (HttpMethod.Put, "/draft", new { json = "{}", file.ConcurrencyStamp }),
        "publish" => (HttpMethod.Post, "/releases", new { file.DraftRevision, file.ConcurrencyStamp }),
        "rollback" => (HttpMethod.Post, "/rollback", new { sourceVersion = 1, file.ConcurrencyStamp }),
        "rename" => (HttpMethod.Put, "", new { name = "renamed.json", file.ConcurrencyStamp }),
        _ => (HttpMethod.Delete, $"?concurrencyStamp={file.ConcurrencyStamp}", null)
    };

    /// <summary>验证错误响应具有统一结构且禁止缓存。</summary>
    /// <param name="client">客户端。</param>
    /// <param name="method">请求方法。</param>
    /// <param name="path">请求路径。</param>
    /// <param name="body">可选请求正文。</param>
    /// <param name="expected">预期错误状态。</param>
    /// <returns>验证任务。</returns>
    internal static async Task ExpectAsync(HttpClient client, HttpMethod method, string path, object? body, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal((int)expected, problem.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("traceId").GetString()));
    }
}
