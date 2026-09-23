using System.Net;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Trelix.Server.Features.ConfigFiles;
using Trelix.Server.Features.Distribution;
using Trelix.Server.Features.Projects;
using Trelix.Server.Features.Releases;
using Trelix.Server.Infrastructure.Notifications;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;
using static Trelix.Server.Tests.ConfigurationApiFixture;

namespace Trelix.Server.Tests;

/// <summary>通过真实 HTTP 和 SQLite 验证长轮询的一致性、身份及资源释放。</summary>
public sealed class DistributionWatchTests
{
    /// <summary>实际进入等待注册之前，认证及发布查询创建的所有 DbContext 都已经释放。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task WaitingDoesNotRetainDatabaseContexts()
    {
        var contexts = new ConcurrentBag<TrelixDbContext>();
        await using var fixture = await CreateAsync(services =>
        {
            Configure(services);
            services.Replace(ServiceDescriptor.Scoped(provider =>
            {
                var db = new TrelixDbContext(provider.GetRequiredService<DbContextOptions<TrelixDbContext>>());
                contexts.Add(db);
                return db;
            }));
        });
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        fixture.App.Services.GetRequiredService<ObservedNotifications>().BeforeSubscribe = () =>
        {
            Assert.NotEmpty(contexts);
            Assert.All(contexts, db => Assert.Throws<ObjectDisposedException>(() => db.Entry(new ConfigFile { Name = "probe" })));
        };
        using var response = await consumer.GetAsync(Path(fixture), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>其他文件的通知不能唤醒当前文件，服务停止取消现有等待并清理注册。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task NotificationsAreIsolatedAndHostStopReleasesWaiters()
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        var pending = consumer.GetAsync(Path(fixture), TestContext.Current.CancellationToken);
        await RegisteredAsync(fixture);
        var notifications = fixture.App.Services.GetRequiredService<ReleaseNotifications>();
        notifications.Notify(Guid.NewGuid());
        Assert.Equal(1, notifications.ActiveCount);
        fixture.App.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>().StopApplication();
        using var response = await pending;
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, notifications.ActiveCount);
    }

    /// <summary>使用注册信号同步测试操作，并允许模拟通知丢失和注册窗口内提交。</summary>
    /// <param name="inner">真实有界等待管理器。</param>
    private sealed class ObservedNotifications(ReleaseNotifications inner) : IReleaseNotifications
    {
        internal Channel<Guid> Registered { get; } = Channel.CreateUnbounded<Guid>();
        internal Action? BeforeSubscribe { get; set; }
        internal bool DropNotifications { get; set; }
        internal bool ThrowOnNotify { get; set; }
        internal int NotificationCount { get; private set; }

        /// <inheritdoc />
        public ReleaseSubscription Subscribe(Guid fileId)
        {
            BeforeSubscribe?.Invoke();
            var subscription = inner.Subscribe(fileId);
            Registered.Writer.TryWrite(fileId);
            return subscription;
        }

        /// <inheritdoc />
        public void Notify(Guid fileId)
        {
            NotificationCount++;
            if (ThrowOnNotify) throw new InvalidOperationException("sensitive-notification-error");
            if (!DropNotifications) inner.Notify(fileId);
        }
    }

    /// <summary>注册观测器并配置较短的正常等待期限，保持生产默认值不变。</summary>
    /// <param name="services">测试服务集合。</param>
    private static void Configure(IServiceCollection services)
    {
        services.Configure<DistributionOptions>(x => { x.WaitTimeout = TimeSpan.FromMilliseconds(500); x.MaxConcurrentListeners = 1; });
        services.AddSingleton<ObservedNotifications>();
        services.Replace(ServiceDescriptor.Singleton<IReleaseNotifications>(sp => sp.GetRequiredService<ObservedNotifications>()));
    }

    /// <summary>构建正式监听请求，按已知文件和版本传递发布身份。</summary>
    /// <param name="fixture">已初始化的资源。</param>
    /// <param name="version">已知版本。</param>
    /// <param name="id">可选旧文件标识。</param>
    /// <returns>监听相对 URI。</returns>
    private static string Path(ConfigurationApiFixture fixture, long version = 1, Guid? id = null) =>
        fixture.ReadPath.Replace("configuration?", "configuration/watch?", StringComparison.Ordinal)
        + $"&knownConfigFileId={id ?? fixture.File.Id}&knownVersion={version}";

    /// <summary>等待真实等待者注册，避免依赖调度延时安排发布。</summary>
    /// <param name="fixture">测试宿主。</param>
    /// <returns>注册观察任务。</returns>
    private static async Task RegisteredAsync(ConfigurationApiFixture fixture) =>
        await fixture.App.Services.GetRequiredService<ObservedNotifications>().Registered.Reader.ReadAsync(TestContext.Current.CancellationToken);

    /// <summary>验证发布与回滚在事务提交后唤醒，返回身份对应可读取的正文。</summary>
    /// <param name="rollback">是否通过回滚产生新版本。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommittedReleaseWakesAndCanBeRead(bool rollback)
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{\"value\":1}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        var pending = consumer.GetAsync(Path(fixture), TestContext.Current.CancellationToken);
        await RegisteredAsync(fixture);
        if (rollback)
            await SendAsync<PublicationResponse>(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/rollback",
                new { sourceVersion = 1, fixture.File.ConcurrencyStamp }, HttpStatusCode.Created);
        else
        {
            await fixture.SaveAsync("{\"value\":2}");
            await fixture.PublishAsync();
        }
        using var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var identity = await response.Content.ReadFromJsonAsync<PublishedIdentityResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(new(fixture.File.Id, 2), identity);
        var read = await SendAsync<PublishedConfigResponse>(consumer, HttpMethod.Get, fixture.ReadPath);
        Assert.Equal(identity!.Version, read.Version);
        Assert.Equal(0, fixture.App.Services.GetRequiredService<ReleaseNotifications>().ActiveCount);
    }

    /// <summary>正常超时、保存草稿和事务失败都不会制造发布通知。</summary>
    /// <param name="operation">监听期间执行的管理操作。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("none")]
    [InlineData("draft")]
    [InlineData("failed-publication")]
    public async Task NoReleaseReturnsNoContent(string operation)
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        var notifications = fixture.App.Services.GetRequiredService<ObservedNotifications>();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        var pending = consumer.GetAsync(Path(fixture), TestContext.Current.CancellationToken);
        await RegisteredAsync(fixture);
        if (operation != "none") await fixture.SaveAsync("{\"draft\":true}");
        if (operation == "failed-publication")
        {
            await fixture.App.WithDbAsync(db => db.Database.ExecuteSqlRawAsync(
                "CREATE TRIGGER RejectWatchPublish BEFORE INSERT ON Releases BEGIN SELECT RAISE(ABORT, 'injected'); END;",
                TestContext.Current.CancellationToken));
            await ConfigurationPublishingTests.ExpectAsync(fixture.Admin, HttpMethod.Post, fixture.FilePath + "/releases",
                new { fixture.File.DraftRevision, fixture.File.ConcurrencyStamp }, HttpStatusCode.InternalServerError);
        }
        using var response = await pending;
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(1, notifications.NotificationCount);
        Assert.Equal(0, fixture.App.Services.GetRequiredService<ReleaseNotifications>().ActiveCount);
    }

    /// <summary>提交后丢失或抛异常的通知不改变发布成功结果，正常超时仍发现更新。</summary>
    /// <param name="throwFailure">是否模拟通知抛异常。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissedNotificationIsRecoveredFromStorage(bool throwFailure)
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        var notifications = fixture.App.Services.GetRequiredService<ObservedNotifications>();
        notifications.DropNotifications = true;
        notifications.ThrowOnNotify = throwFailure;
        var pending = consumer.GetAsync(Path(fixture), TestContext.Current.CancellationToken);
        await RegisteredAsync(fixture);
        await fixture.PublishAsync();
        using var response = await pending;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(fixture.App.Logs.Entries, x => x.Contains("sensitive-notification-error", StringComparison.Ordinal));
    }

    /// <summary>首次核对与等待者注册之间完成的持久化发布不会漏报。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task ReleaseBetweenCheckAndRegistrationIsObserved()
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        var notifications = fixture.App.Services.GetRequiredService<ObservedNotifications>();
        notifications.BeforeSubscribe = () =>
        {
            // 此受控同步点用真实 SQLite 提交模拟另一请求已完成的发布，不执行异步阻塞。
            using var scope = fixture.App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TrelixDbContext>();
            using var transaction = db.Database.BeginTransaction();
            db.Releases.Add(new Release { ConfigFileId = fixture.File.Id, Version = 2, DraftRevision = 1,
                PublishedAt = fixture.App.Clock.GetUtcNow(), Json = "{}" });
            db.SaveChanges();
            db.ConfigFiles.Single(x => x.Id == fixture.File.Id).CurrentReleaseVersion = 2;
            db.SaveChanges();
            transaction.Commit();
        };
        using var response = await consumer.GetAsync(Path(fixture), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<PublishedIdentityResponse>(TestContext.Current.CancellationToken))!.Version);
    }

    /// <summary>等待期间凭证撤销、过期或授权移除会在返回前被重新查询。</summary>
    /// <param name="change">身份或权限变化。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("revoke")]
    [InlineData("expire")]
    [InlineData("scope")]
    public async Task WaitingRequestRevalidatesCredentials(string change)
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        var issued = await fixture.IssueAsync();
        using var consumer = fixture.Consumer(issued.Secret);
        var pending = consumer.GetAsync(Path(fixture), TestContext.Current.CancellationToken);
        await RegisteredAsync(fixture);
        if (change == "expire") fixture.App.Clock.Advance(TimeSpan.FromHours(2));
        else if (change == "revoke")
        {
            using var revoke = await fixture.Admin.PostAsync($"/api/admin/application-tokens/{issued.Token.Id}/revoke", null, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        }
        else await fixture.App.WithDbAsync(db => db.TokenScopes.Where(x => x.ApplicationTokenId == issued.Token.Id).ExecuteDeleteAsync(TestContext.Current.CancellationToken));
        fixture.App.Services.GetRequiredService<ReleaseNotifications>().Notify(fixture.File.Id);
        using var response = await pending;
        Assert.Equal(change == "scope" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>取消请求会释放容量，容量耗尽有明确的 503 响应。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task CancellationAndCapacityAreBounded()
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = consumer.GetAsync(Path(fixture), cancel.Token);
        await RegisteredAsync(fixture);
        await ConfigurationPublishingTests.ExpectAsync(consumer, HttpMethod.Get, Path(fixture), null, HttpStatusCode.ServiceUnavailable);
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { using var response = await pending; });
        Assert.Equal(0, fixture.App.Services.GetRequiredService<ReleaseNotifications>().ActiveCount);
    }

    /// <summary>真实监听入口拒绝无凭证和错误身份，缺失发布身份也不会开始等待。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task WatchValidatesAuthenticationScopeAndIdentity()
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var anonymous = fixture.Consumer(null);
        using var invalid = fixture.Consumer("invalid-token");
        foreach (var client in new[] { anonymous, invalid, fixture.Admin })
            await ConfigurationPublishingTests.ExpectAsync(client, HttpMethod.Get, Path(fixture), null, HttpStatusCode.Unauthorized);
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        await ConfigurationPublishingTests.ExpectAsync(consumer, HttpMethod.Get, Path(fixture, 0), null, HttpStatusCode.BadRequest);
        await ConfigurationPublishingTests.ExpectAsync(consumer, HttpMethod.Get, Path(fixture, 1, Guid.Empty), null, HttpStatusCode.BadRequest);
        await ConfigurationPublishingTests.ExpectAsync(consumer, HttpMethod.Get, Path(fixture).Split("&knownConfigFileId")[0], null, HttpStatusCode.BadRequest);
        var other = await fixture.App.SeedResourcesAsync();
        var sameProjectEnvironment = await SendAsync<EnvironmentResponse>(fixture.Admin, HttpMethod.Post,
            $"/api/admin/projects/{fixture.Project.Id}/environments", new { key = "isolated", displayName = "Isolated" }, HttpStatusCode.Created);
        await ConfigurationPublishingTests.ExpectAsync(consumer, HttpMethod.Get,
            Path(fixture).Replace("environmentKey=" + fixture.Environment.Key, "environmentKey=" + sameProjectEnvironment.Key, StringComparison.Ordinal),
            null, HttpStatusCode.Forbidden);
        foreach (var pair in new[] { (other.First.Key, other.FirstEnv.Key), (other.Second.Key, other.SecondEnv.Key) })
            await ConfigurationPublishingTests.ExpectAsync(consumer, HttpMethod.Get,
                $"/api/application/configuration/watch?projectKey={pair.Item1}&environmentKey={pair.Item2}&fileName=app.json&knownConfigFileId={fixture.File.Id}&knownVersion=1",
                null, HttpStatusCode.Forbidden);
    }

    /// <summary>删除后同名重建返回新文件身份，即使发布版本重新从一开始。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task RecreatedFileUsesNewIdentity()
    {
        await using var fixture = await CreateAsync(Configure);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var consumer = fixture.Consumer((await fixture.IssueAsync()).Secret);
        var old = fixture.File.Id;
        using var deletion = await fixture.Admin.DeleteAsync(fixture.FilePath + "?concurrencyStamp=" + fixture.File.ConcurrencyStamp, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, deletion.StatusCode);
        await ConfigurationPublishingTests.ExpectAsync(consumer, HttpMethod.Get, Path(fixture), null, HttpStatusCode.NotFound);
        fixture.File = await SendAsync<ConfigFileResponse>(fixture.Admin, HttpMethod.Post, fixture.EnvironmentPath + "/files", new { name = "app.json" }, HttpStatusCode.Created);
        await fixture.SaveAsync("{}");
        await fixture.PublishAsync();
        using var response = await consumer.GetAsync(Path(fixture, 1, old), TestContext.Current.CancellationToken);
        var identity = await response.Content.ReadFromJsonAsync<PublishedIdentityResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(new(fixture.File.Id, 1), identity);
        Assert.NotEqual(old, identity!.ConfigFileId);
    }
}
