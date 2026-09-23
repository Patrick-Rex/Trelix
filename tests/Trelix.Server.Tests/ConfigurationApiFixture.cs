using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using Microsoft.Extensions.DependencyInjection;

namespace Trelix.Server.Tests;

/// <summary>通过真实管理 API 建立配置资源，提供独立 SQLite 与请求辅助。</summary>
public sealed class ConfigurationApiFixture : IAsyncDisposable
{
    private readonly TestDataDirectory data = new();
    public ServerFactory App { get; }
    public HttpClient Admin { get; }
    public ProjectResponse Project { get; private set; } = null!;
    public EnvironmentResponse Environment { get; private set; } = null!;
    public ConfigFileResponse File { get; set; } = null!;
    public string EnvironmentPath => $"/api/admin/projects/{Project.Id}/environments/{Environment.Id}";
    public string FilePath => $"{EnvironmentPath}/files/{File.Id}";
    public string ReadPath => $"/api/application/configuration?projectKey={Uri.EscapeDataString(Project.Key)}&environmentKey={Uri.EscapeDataString(Environment.Key)}&fileName={Uri.EscapeDataString(File.Name)}";

    /// <summary>创建测试宿主及保存 Cookie 的管理客户端。</summary>
    /// <param name="configure">可选测试服务配置。</param>
    private ConfigurationApiFixture(Action<IServiceCollection>? configure)
    {
        App = new ServerFactory(data) { ConfigureTestServices = configure };
        Admin = App.NewClient();
    }

    /// <summary>登录并通过 HTTP 创建项目、环境和空文件。</summary>
    /// <returns>拥有完整资源路径的测试夹具。</returns>
    /// <param name="configure">可选测试服务配置。</param>
    public static async Task<ConfigurationApiFixture> CreateAsync(Action<IServiceCollection>? configure = null)
    {
        var fixture = new ConfigurationApiFixture(configure);
        try
        {
            await fixture.App.LoginAsync(fixture.Admin);
            fixture.Project = await SendAsync<ProjectResponse>(fixture.Admin, HttpMethod.Post, "/api/admin/projects",
                new { key = "project", displayName = "Project" }, HttpStatusCode.Created);
            fixture.Environment = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Post,
                $"/api/admin/projects/{fixture.Project.Id}/environments", new { key = "env", displayName = "Environment" }, HttpStatusCode.Created);
            fixture.File = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post,
                fixture.EnvironmentPath + "/files", new { name = "app.json" }, HttpStatusCode.Created);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    /// <summary>发送 JSON 请求并验证状态、类型和成功创建的资源位置。</summary>
    /// <typeparam name="T">响应契约。</typeparam>
    /// <param name="client">请求客户端。</param>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">请求路径。</param>
    /// <param name="body">可选 JSON 请求。</param>
    /// <param name="expected">预期状态。</param>
    /// <returns>反序列化后的响应。</returns>
    public static async Task<T> SendAsync<T>(HttpClient client, HttpMethod method, string path, object? body = null,
        HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        if (expected == HttpStatusCode.Created)
            Assert.NotNull(response.Headers.Location);
        return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: TestContext.Current.CancellationToken))!;
    }

    /// <summary>保存新的草稿并更新夹具的并发基准。</summary>
    /// <param name="json">配置正文。</param>
    /// <returns>保存后的草稿快照。</returns>
    public async Task<DraftResponse> SaveAsync(string json)
    {
        var draft = await SendAsync<DraftResponse>(Admin, HttpMethod.Put, FilePath + "/draft", new { json, File.ConcurrencyStamp });
        File = draft.File;
        return draft;
    }

    /// <summary>发布当前草稿并更新夹具的并发基准。</summary>
    /// <returns>发布结果。</returns>
    public async Task<PublicationResponse> PublishAsync()
    {
        var release = await SendAsync<PublicationResponse>(Admin, HttpMethod.Post, FilePath + "/releases",
            new { File.DraftRevision, File.ConcurrencyStamp }, HttpStatusCode.Created);
        File = release.File;
        return release;
    }

    /// <summary>创建限定当前项目环境的应用令牌。</summary>
    /// <returns>用于生命周期测试的签发结果。</returns>
    public Task<IssuedApplicationTokenResponse> IssueAsync() => SendAsync<IssuedApplicationTokenResponse>(Admin,
        HttpMethod.Post, "/api/admin/application-tokens", new CreateApplicationTokenRequest
        {
            Name = "application", ExpiresAt = App.Clock.GetUtcNow().AddHours(1),
            Scopes = [new TokenScopeRequest(Project.Id, Environment.Id)]
        }, HttpStatusCode.Created);

    /// <summary>创建只携带指定 Bearer 的应用客户端。</summary>
    /// <param name="secret">可选应用凭证。</param>
    /// <returns>不使用管理 Cookie 的客户端。</returns>
    public HttpClient Consumer(string? secret)
    {
        var client = App.NewClient(false);
        if (secret is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return client;
    }

    /// <summary>依序关闭客户端、宿主和测试 SQLite 目录。</summary>
    /// <returns>释放任务。</returns>
    public async ValueTask DisposeAsync()
    {
        Admin.Dispose();
        await App.DisposeAsync();
        data.Dispose();
    }
}
