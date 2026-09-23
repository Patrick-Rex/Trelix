using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Trelix.Server.Features.Authentication;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Tests;

/// <summary>支持原子推进时间的测试时钟，用于验证会话和令牌到期。</summary>
public sealed class TestClock : TimeProvider
{
    private long ticks = DateTimeOffset.UtcNow.UtcTicks;
    /// <summary>读取当前模拟 UTC 时间。</summary>
    /// <returns>线程安全读取的测试时间。</returns>
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    /// <summary>按指定间隔推进模拟时间，无需实际等待。</summary>
    /// <param name="delta">模拟时间推进量。</param>
    public void Advance(TimeSpan delta) => Interlocked.Add(ref ticks, delta.Ticks);
}

/// <summary>收集测试宿主日志，以检查凭证和异常正文是否泄露。</summary>
public sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Entries { get; } = new();
    /// <summary>创建共享同一日志队列的记录器，不按类别过滤。</summary>
    /// <param name="categoryName">日志类别名称；当前收集器不按类别区分。</param>
    /// <returns>将日志写入内存队列的记录器。</returns>
    public ILogger CreateLogger(string categoryName) => new CapturedLogger(Entries);
    /// <summary>完成日志提供程序释放；内存队列无需额外清理。</summary>
    public void Dispose() { }

    /// <summary>将格式化日志及异常文本保存到测试队列，便于检查敏感信息。</summary>
    /// <param name="entries">共享的线程安全日志队列。</param>
    private sealed class CapturedLogger(ConcurrentQueue<string> entries) : ILogger
    {
        /// <summary>忽略日志作用域，保持测试日志收集器无作用域状态。</summary>
        /// <typeparam name="TState">日志状态类型。</typeparam>
        /// <param name="state">日志调用携带的状态。</param>
        /// <returns>始终返回 null。</returns>
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        /// <summary>启用所有日志等级，确保测试能够检查全部输出。</summary>
        /// <param name="logLevel">当前日志等级。</param>
        /// <returns>始终返回 true。</returns>
        public bool IsEnabled(LogLevel logLevel) => true;
        /// <summary>将格式化消息和可选异常文本加入共享队列。</summary>
        /// <typeparam name="TState">日志状态类型。</typeparam>
        /// <param name="logLevel">当前日志等级。</param>
        /// <param name="eventId">当前日志事件标识。</param>
        /// <param name="state">日志调用携带的状态。</param>
        /// <param name="exception">当前待处理或记录的异常。</param>
        /// <param name="formatter">将状态及异常格式化为消息的委托。</param>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(formatter(state, exception) + (exception is null ? "" : exception.ToString()));
    }
}

/// <summary>管理工作区 artifacts 下的独立测试数据目录及安全清理。</summary>
public sealed class TestDataDirectory : IDisposable
{
    private readonly string root;
    public string Path { get; }

    /// <summary>定位解决方案根目录，并创建本次测试独占的数据目录。</summary>
    public TestDataDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(System.IO.Path.Combine(current.FullName, "Trelix.slnx")))
            current = current.Parent;
        if (current is null)
            throw new InvalidOperationException("Tests must run from the Trelix workspace.");
        root = System.IO.Path.GetFullPath(System.IO.Path.Combine(current.FullName, "artifacts", "m2-tests"));
        Path = System.IO.Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>释放 SQLite 连接池并在确认目录边界后清理本次测试数据。</summary>
    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        var target = System.IO.Path.GetFullPath(Path);
        if (!target.StartsWith(root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Refusing cleanup outside test artifacts.");
        if (Directory.Exists(target))
            Directory.Delete(target, recursive: true);
    }
}

/// <summary>创建使用独立 SQLite、随机凭证和可控时钟的集成测试宿主。</summary>
/// <param name="data">本次测试的数据与密钥目录。</param>
public sealed class ServerFactory(TestDataDirectory data) : WebApplicationFactory<Program>
{
    public string Username { get; init; } = "integration-admin";
    public string Password { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public string EnvironmentName { get; init; } = "Development";
    public TestClock Clock { get; } = new();
    public CapturedLogs Logs { get; } = new();

    /// <summary>注入测试存储、凭证和时钟，捕获日志并注册仅供测试的探针。</summary>
    /// <param name="builder">待配置的宿主构建器。</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Trelix:Storage:DataDirectory"] = data.Path,
            ["Trelix:Administrator:Username"] = Username,
            ["Trelix:Administrator:Password"] = Password,
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = ""
        }));
        builder.ConfigureLogging(logging => { logging.ClearProviders(); logging.AddProvider(Logs); });
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<TimeProvider>(Clock));
            services.AddTransient<IStartupFilter, ProbeEndpoints>();
        });
    }

    /// <summary>创建不自动跟随重定向的 HTTPS 测试客户端。</summary>
    /// <param name="handleCookies">是否由测试客户端自动保存并发送 Cookie。</param>
    /// <returns>由测试宿主驱动的 HTTP 客户端。</returns>
    public HttpClient NewClient(bool handleCookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://trelix.invalid"), AllowAutoRedirect = false, HandleCookies = handleCookies
    });

    /// <summary>在独立异步服务作用域中执行数据库操作并释放上下文。</summary>
    /// <typeparam name="T">数据库操作的结果类型。</typeparam>
    /// <param name="action">在独立数据库上下文中执行的异步操作。</param>
    /// <returns>数据库操作返回的结果。</returns>
    public async Task<T> WithDbAsync<T>(Func<TrelixDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TrelixDbContext>());
    }

    /// <summary>建立两个项目和三个环境，以覆盖同名环境及跨项目授权场景。</summary>
    /// <returns>创建的两个项目及其测试环境。</returns>
    public async Task<(Project First, Project Second, ProjectEnvironment FirstEnv, ProjectEnvironment OtherEnv, ProjectEnvironment SecondEnv)> SeedResourcesAsync()
    {
        return await WithDbAsync(async db =>
        {
            var first = new Project { Key = "first", DisplayName = "First" };
            var second = new Project { Key = "second", DisplayName = "Second" };
            var firstEnv = new ProjectEnvironment { ProjectId = first.Id, Key = "shared", DisplayName = "First shared" };
            var otherEnv = new ProjectEnvironment { ProjectId = first.Id, Key = "other", DisplayName = "Other" };
            var secondEnv = new ProjectEnvironment { ProjectId = second.Id, Key = "shared", DisplayName = "Second shared" };
            db.AddRange(first, second, firstEnv, otherEnv, secondEnv);
            await db.SaveChangesAsync(cancellationToken: TestContext.Current.CancellationToken);
            return (first, second, firstEnv, otherEnv, secondEnv);
        });
    }

    /// <summary>获取匿名防伪造令牌、登录，再刷新绑定管理员身份的请求令牌。</summary>
    /// <param name="client">用于当前测试请求的 HTTP 客户端。</param>
    /// <returns>可供重放测试使用的管理员 Cookie 名值对。</returns>
    public async Task<string> LoginAsync(HttpClient client)
    {
        await RefreshCsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest { Username = Username, Password = Password }, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("Trelix.Admin=", StringComparison.Ordinal));
        await RefreshCsrfAsync(client);
        return cookie.Split(';')[0];
    }

    /// <summary>获取当前身份的防伪造请求令牌并更新客户端默认请求头。</summary>
    /// <param name="client">用于当前测试请求的 HTTP 客户端。</param>
    /// <returns>新的防伪造请求令牌。</returns>
    public static async Task<string> RefreshCsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/admin/auth/antiforgery", cancellationToken: TestContext.Current.CancellationToken);
        Assert.NotNull(csrf);
        client.DefaultRequestHeaders.Remove(AuthenticationConstants.CsrfHeader);
        client.DefaultRequestHeaders.Add(AuthenticationConstants.CsrfHeader, csrf.RequestToken);
        return csrf.RequestToken;
    }
}
