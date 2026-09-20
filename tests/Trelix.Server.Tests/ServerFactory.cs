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

public sealed class TestClock : TimeProvider
{
    private long ticks = DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref ticks), TimeSpan.Zero);
    public void Advance(TimeSpan delta) => Interlocked.Add(ref ticks, delta.Ticks);
}

public sealed class CapturedLogs : ILoggerProvider
{
    public ConcurrentQueue<string> Entries { get; } = new();
    public ILogger CreateLogger(string categoryName) => new CapturedLogger(Entries);
    public void Dispose() { }

    private sealed class CapturedLogger(ConcurrentQueue<string> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(formatter(state, exception) + (exception is null ? "" : exception.ToString()));
    }
}

public sealed class TestDataDirectory : IDisposable
{
    private readonly string root;
    public string Path { get; }

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

public sealed class ServerFactory(TestDataDirectory data) : WebApplicationFactory<Program>
{
    public string Username { get; init; } = "integration-admin";
    public string Password { get; init; } = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    public string EnvironmentName { get; init; } = "Development";
    public TestClock Clock { get; } = new();
    public CapturedLogs Logs { get; } = new();

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
            services.AddControllers().AddApplicationPart(typeof(ApplicationProbeController).Assembly);
        });
    }

    public HttpClient NewClient(bool handleCookies = true) => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://trelix.invalid"), AllowAutoRedirect = false, HandleCookies = handleCookies
    });

    public async Task<T> WithDbAsync<T>(Func<TrelixDbContext, Task<T>> action)
    {
        await using var scope = Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<TrelixDbContext>());
    }

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
            await db.SaveChangesAsync();
            return (first, second, firstEnv, otherEnv, secondEnv);
        });
    }

    public async Task<string> LoginAsync(HttpClient client)
    {
        await RefreshCsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/admin/auth/login", new LoginRequest { Username = Username, Password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("Trelix.Admin=", StringComparison.Ordinal));
        await RefreshCsrfAsync(client);
        return cookie.Split(';')[0];
    }

    public static async Task<string> RefreshCsrfAsync(HttpClient client)
    {
        var csrf = await client.GetFromJsonAsync<AntiforgeryResponse>("/api/admin/auth/antiforgery");
        Assert.NotNull(csrf);
        client.DefaultRequestHeaders.Remove(AuthenticationConstants.CsrfHeader);
        client.DefaultRequestHeaders.Add(AuthenticationConstants.CsrfHeader, csrf.RequestToken);
        return csrf.RequestToken;
    }
}
