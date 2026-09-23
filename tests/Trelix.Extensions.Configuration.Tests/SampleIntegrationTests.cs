using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trelix.SampleApp;
using Trelix.Server.Features.ApplicationTokens;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Distribution;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using Trelix.Server.Tests;
using static Trelix.Server.Tests.ConfigurationApiFixture;

namespace Trelix.Extensions.Configuration.Tests;

/// <summary>使用真实 Kestrel、SQLite、SDK HTTP 连接及示例应用验证发布和停机恢复。</summary>
public sealed class SampleIntegrationTests
{
    /// <summary>将 SDK 的失败事件转换成同步信号，不记录配置或凭证。</summary>
    private sealed class FailureSignal : ILoggerProvider, ILogger
    {
        internal TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => this;
        /// <inheritdoc />
        public void Dispose() { }
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (formatter(state, exception).StartsWith("Trelix 配置更新失败", StringComparison.Ordinal)) Failed.TrySetResult();
        }
    }

    /// <summary>连接节与管理界面复制形状一致，验证原生 Options 重载及服务重启后的持久化恢复。</summary>
    /// <returns>端到端测试任务。</returns>
    [Fact]
    public async Task SampleReadsReloadsAndRecoversAcrossRealServerRestart()
    {
        using var data = new TestDataDirectory();
        var factory = new ServerFactory(data)
        {
            ConfigureTestServices = services => services.Configure<DistributionOptions>(options => options.WaitTimeout = TimeSpan.FromMilliseconds(250))
        };
        factory.UseKestrel(0);
        var admin = factory.CreateClient();
        try
        {
            await factory.LoginAsync(admin);
            var project = await SendAsync<ProjectResponse>(admin, HttpMethod.Post, "/api/admin/projects", new { key = "Sample +项目", displayName = "Sample" }, HttpStatusCode.Created);
            var environment = await SendAsync<EnvironmentResponse>(admin, HttpMethod.Post, $"/api/admin/projects/{project.Id}/environments",
                new { key = "Env &环境", displayName = "Env" }, HttpStatusCode.Created);
            var filesPath = $"/api/admin/projects/{project.Id}/environments/{environment.Id}/files";
            var file = await SendAsync<ConfigFileResponse>(admin, HttpMethod.Post, filesPath, new { name = "sample #配置.json" }, HttpStatusCode.Created);
            var filePath = filesPath + "/" + file.Id;
            file = await PublishAsync(admin, filePath, file, "first");
            var issued = await SendAsync<IssuedApplicationTokenResponse>(admin, HttpMethod.Post, "/api/admin/application-tokens", new CreateApplicationTokenRequest
            {
                Name = "sample-test", ExpiresAt = factory.Clock.GetUtcNow().AddHours(1), Scopes = [new TokenScopeRequest(project.Id, environment.Id)]
            }, HttpStatusCode.Created);
            // 与 UI 复制片段使用相同五字段，令牌占位符通过后添加的外部配置覆盖。
            var snippet = JsonSerializer.Serialize(new { Trelix = new
            {
                ServerUrl = admin.BaseAddress!.AbsoluteUri, ProjectKey = project.Key, EnvironmentKey = environment.Key,
                FileName = file.Name, AccessToken = "<APPLICATION_TOKEN>"
            } });
            var snippetPath = System.IO.Path.Combine(data.Path, "connection.json");
            await File.WriteAllTextAsync(snippetPath, snippet, TestContext.Current.CancellationToken);
            var failures = new FailureSignal();
            await using var sample = await SampleApplication.CreateAsync([], builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0");
                builder.Logging.ClearProviders().AddProvider(failures);
                builder.Configuration.AddJsonFile(snippetPath);
                builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Trelix:AccessToken"] = issued.Secret,
                    ["Trelix:ReadTimeout"] = "00:00:02", ["Trelix:WatchTimeout"] = "00:00:02",
                    ["Trelix:RetryMinDelay"] = "00:00:00.050", ["Trelix:RetryMaxDelay"] = "00:00:00.100"
                });
            }, TestContext.Current.CancellationToken);
            var changes = Channel.CreateUnbounded<SampleOptions>();
            using var subscription = sample.Services.GetRequiredService<IOptionsMonitor<SampleOptions>>().OnChange(value => changes.Writer.TryWrite(value));
            await sample.StartAsync(TestContext.Current.CancellationToken);
            using var business = new HttpClient { BaseAddress = new Uri(sample.Urls.Single()) };
            await AssertSampleAsync(business, "first");
            file = await PublishAsync(admin, filePath, file, "second");
            await changes.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await AssertSampleAsync(business, "second");
            var port = admin.BaseAddress!.Port;
            var password = factory.Password;
            admin.Dispose();
            await factory.DisposeAsync();
            await failures.Failed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await AssertSampleAsync(business, "second");

            factory = new ServerFactory(data)
            {
                Password = password,
                ConfigureTestServices = services => services.Configure<DistributionOptions>(options => options.WaitTimeout = TimeSpan.FromMilliseconds(250))
            };
            factory.UseKestrel(port);
            admin = factory.CreateClient();
            await factory.LoginAsync(admin);
            file = await PublishAsync(admin, filePath, file, "after-restart");
            await changes.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
            await AssertSampleAsync(business, "after-restart");
            Assert.Equal(3, file.CurrentReleaseVersion);
            await sample.StopAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            admin.Dispose();
            await factory.DisposeAsync();
        }
    }

    /// <summary>通过真实管理入口保存并发布示例正文，返回新并发基准。</summary>
    /// <param name="admin">管理员客户端。</param>
    /// <param name="path">文件管理路径。</param>
    /// <param name="file">当前文件状态。</param>
    /// <param name="message">公开示例字段值。</param>
    /// <returns>发布后的文件状态。</returns>
    private static async Task<ConfigFileResponse> PublishAsync(HttpClient admin, string path, ConfigFileResponse file, string message)
    {
        var draft = await SendAsync<DraftResponse>(admin, HttpMethod.Put, path + "/draft",
            new { json = JsonSerializer.Serialize(new { Sample = new { Message = message, Enabled = true } }), file.ConcurrencyStamp });
        var publication = await SendAsync<PublicationResponse>(admin, HttpMethod.Post, path + "/releases",
            new { draft.File.DraftRevision, draft.File.ConcurrencyStamp }, HttpStatusCode.Created);
        return publication.File;
    }

    /// <summary>验证示例只暴露有限字段，三种原生配置消费方式得到相同的新值。</summary>
    /// <param name="client">实际示例 HTTP 客户端。</param>
    /// <param name="message">期望的示例值。</param>
    /// <returns>断言任务。</returns>
    private static async Task AssertSampleAsync(HttpClient client, string message)
    {
        var response = await client.GetFromJsonAsync<SampleResponse>("/configuration", TestContext.Current.CancellationToken);
        Assert.Equal(message, response!.Configuration);
        Assert.Equal(message, response.Monitor.Message);
        Assert.Equal(message, response.Snapshot.Message);
        Assert.True(response.Monitor.Enabled);
    }
}
