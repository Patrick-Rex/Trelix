using System.Net;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trelix.Server.Tests;

namespace Trelix.Extensions.Configuration.Tests;

/// <summary>验证现代宿主的首次接入、配置优先级、原生重载和生命周期。</summary>
public sealed class ConfigurationTests
{
    /// <summary>测试使用的可重载业务配置。</summary>
    public sealed class BusinessOptions
    {
        /// <summary>用来观察重载的字段。</summary>
        public string? Message { get; set; }
        /// <summary>用于验证 .NET 10 空数组绑定。</summary>
        public string?[]? Items { get; set; }
    }

    /// <summary>两种默认现代宿主保留原配置对象，远程位于业务环境和命令行之下。</summary>
    /// <param name="web">是否使用 WebApplicationBuilder。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ModernHostsHonorAllPriorityLayers(bool web)
    {
        using var directory = new TestDataDirectory();
        var prefix = "M5_" + Guid.NewGuid().ToString("N");
        var environmentKey = prefix + "__Environment";
        Environment.SetEnvironmentVariable(environmentKey, "environment");
        try
        {
            await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "appsettings.json"),
                "{\"" + prefix + "\":{\"Local\":\"base\",\"Specific\":\"base\",\"Secret\":\"base\",\"Remote\":\"base\",\"Environment\":\"base\",\"Command\":\"base\"}}", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "appsettings.Test.json"),
                "{\"" + prefix + "\":{\"Specific\":\"specific\"}}", TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "secrets.json"),
                "{\"" + prefix + "\":{\"Secret\":\"secret\",\"Remote\":\"secret\"}}", TestContext.Current.CancellationToken);
            var args = new[] { "--" + prefix + ":Command=command" };
            IHostApplicationBuilder builder = web
                ? WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = directory.Path, EnvironmentName = "Test" })
                : Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = directory.Path, EnvironmentName = "Test" });
            var configuration = builder.Configuration;
            using var configurationLifetime = (IDisposable)configuration;
            // User Secrets 同样是 JSON 配置源；测试用工作区内的独立文件，不读取用户真实机密。
            var index = configuration.Sources.Select((source, position) => (source, position))
                .Last(x => x.source is EnvironmentVariablesConfigurationSource { Prefix: null or "" }).position;
            configuration.Sources.Insert(index, new JsonConfigurationSource { Path = "secrets.json" });
            using var transport = new ScriptedTransport();
            var original = builder.Configuration;
            await transport.RegisterAsync(builder, Guid.NewGuid(), "{\"" + prefix + "\":{\"Remote\":\"remote\",\"Environment\":\"remote\",\"Command\":\"remote\"}}");
            Assert.Same(original, builder.Configuration);
            Assert.Equal("base", configuration[prefix + ":Local"]);
            Assert.Equal("specific", configuration[prefix + ":Specific"]);
            Assert.Equal("secret", configuration[prefix + ":Secret"]);
            Assert.Equal("remote", configuration[prefix + ":Remote"]);
            Assert.Equal("environment", configuration[prefix + ":Environment"]);
            Assert.Equal("command", configuration[prefix + ":Command"]);
        }
        finally { Environment.SetEnvironmentVariable(environmentKey, null); }
    }

    /// <summary>重载更新 Monitor 和新作用域 Snapshot，移除旧键并保留普通 Options 的启动值。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task ReloadReplacesWholeSnapshotAndUpdatesNativeOptions()
    {
        var id = Guid.NewGuid();
        var builder = Host.CreateApplicationBuilder();
        using var transport = new ScriptedTransport();
        await transport.RegisterAsync(builder, id, "{\"Sample\":{\"Message\":\"first\",\"Removed\":\"old\"}}");
        builder.Services.Configure<BusinessOptions>(builder.Configuration.GetSection("Sample"));
        using var host = builder.Build();
        var ordinary = host.Services.GetRequiredService<IOptions<BusinessOptions>>().Value;
        using var oldScope = host.Services.CreateScope();
        var oldSnapshot = oldScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<BusinessOptions>>().Value;
        var changed = Channel.CreateUnbounded<BusinessOptions>();
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<BusinessOptions>>();
        using var subscription = monitor.OnChange(value => changed.Writer.TryWrite(value));
        await host.StartAsync(TestContext.Current.CancellationToken);
        (await transport.NextAsync()).Changed(id, 2);
        (await transport.NextAsync()).Publish(id, 2, "{\"Sample\":{\"Message\":null,\"Items\":[]}}");
        await changed.Reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Null(builder.Configuration["Sample:Removed"]);
        Assert.Null(monitor.CurrentValue.Message);
        Assert.Empty(monitor.CurrentValue.Items!);
        using var newScope = host.Services.CreateScope();
        Assert.Null(newScope.ServiceProvider.GetRequiredService<IOptionsSnapshot<BusinessOptions>>().Value.Message);
        Assert.Equal("first", oldSnapshot.Message);
        Assert.Equal("first", ordinary.Message);
        await host.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(transport.Disposed);
    }

    /// <summary>源码重建不会断开接入，远程及本地后续连接配置不改变冻结的请求目标。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task SourceRebuildAndConnectionChangesKeepFrozenConnection()
    {
        var builder = Host.CreateApplicationBuilder();
        using var transport = new ScriptedTransport();
        var id = Guid.NewGuid();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Trelix:ProjectKey"] = "local-project" });
        TrelixOptions? configured = null;
        var setup = TrelixHostExtensions.AddCoreAsync(builder, options =>
        {
            Assert.Equal("local-project", options.ProjectKey);
            ScriptedTransport.Configure(options);
            configured = options;
        }, transport, TimeProvider.System, TestContext.Current.CancellationToken);
        (await transport.NextAsync()).Publish(id, 1, "{\"Trelix\":{\"ServerUrl\":\"https://untrusted.invalid\",\"AccessToken\":\"untrusted\"}}");
        Assert.Same(builder, await setup);
        configured!.ServerUrl = "https://changed.invalid/";
        configured.AccessToken = "changed-after-registration";
        builder.Configuration.Sources.Insert(0, new JsonStreamConfigurationSource { Stream = new MemoryStream("{}"u8.ToArray()) });
        builder.Configuration["Trelix:AccessToken"] = "changed-locally";
        await Assert.ThrowsAsync<InvalidOperationException>(() => builder.AddTrelixAsync(cancellationToken: TestContext.Current.CancellationToken));
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var watch = await transport.NextAsync();
        Assert.Equal("trelix.invalid", watch.Uri.Host);
        Assert.StartsWith("/prefix/api/application/configuration/watch", watch.Uri.AbsolutePath);
        Assert.Contains(Uri.EscapeDataString("Project +/中文"), watch.Uri.Query);
        Assert.Equal("test-only-application-token", watch.Token);
        Assert.False(transport.Disposed);
        await host.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(transport.Disposed);
    }

    /// <summary>首次请求失败不能注册远程源或静默回退，诊断不回显底层正文。</summary>
    /// <param name="failure">首次失败类型。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("401")]
    [InlineData("403")]
    [InlineData("404")]
    [InlineData("500")]
    [InlineData("transport")]
    [InlineData("json")]
    [InlineData("metadata")]
    [InlineData("timeout")]
    [InlineData("size")]
    public async Task InitialFailureRejectsStartupAndCleansUp(string failure)
    {
        var builder = Host.CreateApplicationBuilder();
        using var configuration = builder.Configuration;
        using var transport = new ScriptedTransport();
        var pending = TrelixHostExtensions.AddCoreAsync(builder, options =>
        {
            ScriptedTransport.Configure(options);
            if (failure == "timeout") options.ReadTimeout = TimeSpan.FromMilliseconds(50);
        }, transport, TimeProvider.System, TestContext.Current.CancellationToken);
        var request = await transport.NextAsync();
        if (int.TryParse(failure, out var status)) request.Reply((HttpStatusCode)status, "sensitive-response-body");
        else if (failure == "transport") request.Response.SetException(new HttpRequestException("sensitive-transport-error"));
        else if (failure == "json") request.Publish(Guid.NewGuid(), 1, "{\"sensitive-response-body\":");
        else if (failure == "metadata") request.Reply(HttpStatusCode.OK, "{}");
        else if (failure == "size") request.Reply(HttpStatusCode.OK, new string('x', 8 * 1024 * 1024 + 1));
        var error = await Assert.ThrowsAsync<TrelixConfigurationException>(async () => await pending);
        Assert.DoesNotContain("sensitive", error.ToString());
        Assert.DoesNotContain(configuration.Sources, x => x is TrelixConfigurationSource);
        Assert.True(transport.Disposed);
    }

    /// <summary>运行期失败或旧响应保留已加载配置；后续正常请求仍可更新。</summary>
    /// <param name="failure">要注入的故障。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("watch")]
    [InlineData("read")]
    [InlineData("invalid")]
    [InlineData("duplicate")]
    [InlineData("stale")]
    public async Task RuntimeFailureRetainsSnapshotAndRecovers(string failure)
    {
        var id = Guid.NewGuid();
        var builder = Host.CreateApplicationBuilder();
        var logs = new CapturedLogs();
        builder.Logging.ClearProviders().AddProvider(logs);
        using var transport = new ScriptedTransport();
        await transport.RegisterAsync(builder, id, version: 2);
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var initialWatch = await transport.NextAsync();
        if (failure == "watch") initialWatch.Reply(HttpStatusCode.Unauthorized, "sensitive-failure");
        else
        {
            initialWatch.Changed(id, 3);
            var read = await transport.NextAsync();
            if (failure == "read") read.Response.SetException(new HttpRequestException("sensitive-failure"));
            else if (failure == "invalid") read.Publish(id, 2, "{\"sensitive-failure\":");
            else if (failure == "duplicate") read.Publish(id, 2, "{\"Sample\":{\"Message\":\"bad\"}}");
            else read.Publish(id, 1, "{}");
        }
        var retry = await transport.NextAsync();
        Assert.Equal("first", builder.Configuration["Sample:Message"]);
        retry.Changed(id, 3);
        (await transport.NextAsync()).Publish(id, 3, "{\"Sample\":{\"Message\":\"recovered\"}}");
        await transport.NextAsync();
        Assert.Equal("recovered", builder.Configuration["Sample:Message"]);
        Assert.DoesNotContain(logs.Entries, x => x.Contains("sensitive-failure", StringComparison.Ordinal));
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>同名新文件可从版本一接入；正常 204 继续监听且不重复重载。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task RecreatedFileAndNormalTimeoutKeepListening()
    {
        var builder = Host.CreateApplicationBuilder();
        using var transport = new ScriptedTransport();
        await transport.RegisterAsync(builder, Guid.NewGuid());
        using var host = builder.Build();
        var reloads = 0;
        using var registration = Microsoft.Extensions.Primitives.ChangeToken.OnChange(((IConfiguration)builder.Configuration).GetReloadToken, () => Interlocked.Increment(ref reloads));
        await host.StartAsync(TestContext.Current.CancellationToken);
        (await transport.NextAsync()).Reply(HttpStatusCode.NoContent);
        var next = await transport.NextAsync();
        Assert.Equal(0, reloads);
        next.Reply(HttpStatusCode.NotFound);
        next = await transport.NextAsync();
        Assert.Equal("first", builder.Configuration["Sample:Message"]);
        Assert.Equal(0, reloads);
        var newId = Guid.NewGuid();
        next.Changed(newId, 1);
        (await transport.NextAsync()).Publish(newId, 1, "{\"Sample\":{\"Message\":\"new-file\"}}");
        var updated = await transport.NextAsync();
        Assert.Contains(newId.ToString(), updated.Uri.Query);
        Assert.Equal("new-file", builder.Configuration["Sample:Message"]);
        Assert.Equal(1, reloads);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>首次接入取消会释放传输；尚未启动的宿主释放时同样清理连接池。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task CancellationAndUnstartedHostDisposeTransport()
    {
        var builder = Host.CreateApplicationBuilder();
        using var configuration = builder.Configuration;
        using var transport = new ScriptedTransport();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var pending = TrelixHostExtensions.AddCoreAsync(builder, ScriptedTransport.Configure, transport, TimeProvider.System, cancel.Token);
        await transport.NextAsync();
        cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await pending);
        Assert.True(transport.Disposed);
        using var second = new ScriptedTransport();
        await second.RegisterAsync(builder, Guid.NewGuid());
        var host = builder.Build();
        host.Dispose();
        Assert.True(second.Disposed);
    }

    /// <summary>每次指数退避均在配置区间内，避免失败后立即忙循环或无限增长。</summary>
    [Fact]
    public void RetryDelayStaysWithinBounds()
    {
        var options = new TrelixOptions();
        foreach (var failure in new[] { 1, 2, 10, 30, int.MaxValue })
            foreach (var jitter in new[] { 0d, 0.5d, 1d })
                Assert.InRange(TrelixListener.RetryDelay(options, failure, jitter), options.RetryMinDelay, options.RetryMaxDelay);
    }

    /// <summary>后台实际使用可取消的退避；受控触发定时器后才重试，停止会释放未到期定时器。</summary>
    /// <returns>测试任务。</returns>
    [Fact]
    public async Task BackoffIsAwaitedAndHostStopCancelsIt()
    {
        var builder = Host.CreateApplicationBuilder();
        using var transport = new ScriptedTransport();
        var clock = new ControlledTimeProvider();
        var id = Guid.NewGuid();
        var setup = TrelixHostExtensions.AddCoreAsync(builder, ScriptedTransport.Configure, transport, clock, TestContext.Current.CancellationToken);
        (await transport.NextAsync()).Publish(id, 1, "{}");
        await setup;
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        (await transport.NextAsync()).Reply(HttpStatusCode.ServiceUnavailable);
        var firstDelay = await clock.NextAsync();
        Assert.InRange(firstDelay.DueTime, TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2));
        firstDelay.Fire();
        (await transport.NextAsync()).Reply(HttpStatusCode.ServiceUnavailable);
        var secondDelay = await clock.NextAsync();
        await host.StopAsync(TestContext.Current.CancellationToken);
        Assert.True(secondDelay.Disposed);
        Assert.True(transport.Disposed);
    }

    /// <summary>无效本地连接参数在发出请求前得到稳定的错误类别。</summary>
    /// <param name="invalid">要验证的无效字段。</param>
    /// <returns>测试任务。</returns>
    [Theory]
    [InlineData("address")]
    [InlineData("userinfo")]
    [InlineData("name")]
    [InlineData("token")]
    [InlineData("control")]
    [InlineData("duration")]
    public async Task InvalidLocalOptionsFailBeforeRegistration(string invalid)
    {
        var builder = Host.CreateApplicationBuilder();
        using var configuration = builder.Configuration;
        var error = await Assert.ThrowsAsync<TrelixConfigurationException>(() => builder.AddTrelixAsync(options =>
        {
            ScriptedTransport.Configure(options);
            switch (invalid)
            {
                case "address": options.ServerUrl = "not-an-address"; break;
                case "userinfo": options.ServerUrl = "https://sensitive:password@trelix.invalid"; break;
                case "name": options.FileName = " "; break;
                case "token": options.AccessToken = "<APPLICATION_TOKEN>"; break;
                case "control": options.AccessToken = "sensitive\0value"; break;
                case "duration": options.ReadTimeout = TimeSpan.Zero; break;
            }
        }, TestContext.Current.CancellationToken));
        Assert.Equal("invalid_options", error.Code);
        Assert.DoesNotContain("sensitive", error.ToString());
        Assert.DoesNotContain(configuration.Sources, x => x is TrelixConfigurationSource);
    }

    /// <summary>无效 JSON 被完整拒绝，解析过程中不暴露部分数据。</summary>
    /// <param name="json">不兼容的配置正文。</param>
    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"A\":1,\"a\":2}")]
    [InlineData("{\"items\":[{\"a:b\":1}]}")]
    [InlineData("{/*comment*/}")]
    [InlineData("{\"a\":1,}")]
    [InlineData("{\"a\":\"\\uD800\"}")]
    public void InvalidJsonIsRejected(string json) =>
        Assert.Throws<TrelixConfigurationException>(() => ConfigurationSnapshot.Parse(Guid.NewGuid(), 1, json));

    /// <summary>验证大小、深度的合法边界及 .NET 10 的空值/空数组展开。</summary>
    [Fact]
    public void JsonBoundariesAndDotnet10ValuesArePreserved()
    {
        var id = Guid.NewGuid();
        var boundary = "{\"a\":\"" + new string('a', 1024 * 1024 - 8) + "\"}";
        Assert.Single(ConfigurationSnapshot.Parse(id, 1, boundary).Data);
        Assert.Throws<TrelixConfigurationException>(() => ConfigurationSnapshot.Parse(id, 1, boundary + " "));
        var deep = string.Concat(Enumerable.Repeat("{\"a\":", 64)) + "1" + new string('}', 64);
        Assert.Single(ConfigurationSnapshot.Parse(id, 1, deep).Data);
        Assert.Throws<TrelixConfigurationException>(() => ConfigurationSnapshot.Parse(id, 1, "{\"a\":" + deep + "}"));
        var data = ConfigurationSnapshot.Parse(id, 1, "{\"a\":null,\"b\":[],\"c\":{},\"d\":[null,\"中文\",true,1.25]}").Data;
        Assert.Null(data["a"]);
        Assert.Equal("", data["b"]);
        Assert.Null(data["c"]);
        Assert.Null(data["d:0"]);
        Assert.Equal("中文", data["D:1"]);
        Assert.Equal("1.25", data["d:3"]);
    }
}
