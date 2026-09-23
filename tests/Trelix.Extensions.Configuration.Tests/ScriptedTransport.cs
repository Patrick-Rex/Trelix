using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Trelix.Extensions.Configuration;

namespace Trelix.Extensions.Configuration.Tests;

/// <summary>以显式请求/响应握手控制 SDK 网络顺序，不依赖任意等待时间。</summary>
internal sealed class ScriptedTransport : HttpMessageHandler
{
    private readonly Channel<Exchange> requests = Channel.CreateUnbounded<Exchange>();
    internal bool Disposed { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var exchange = new Exchange(request.RequestUri!, request.Headers.Authorization?.Parameter);
        await requests.Writer.WriteAsync(exchange, cancellationToken);
        return await exchange.Response.Task.WaitAsync(cancellationToken);
    }

    /// <summary>取得下一请求，有限超时使故障可以明确结束。</summary>
    /// <returns>可以由测试决定响应的请求。</returns>
    internal async Task<Exchange> NextAsync() =>
        await requests.Reader.ReadAsync(TestContext.Current.CancellationToken).AsTask().WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        Disposed = true;
        base.Dispose(disposing);
    }

    /// <summary>填充不对应真实资源或凭证的 SDK 测试连接选项。</summary>
    /// <param name="options">待填充选项。</param>
    internal static void Configure(TrelixOptions options)
    {
        options.ServerUrl = "https://trelix.invalid/prefix/";
        options.ProjectKey = "Project +/中文";
        options.EnvironmentKey = "ENV #&";
        options.FileName = "app ?配置.json";
        options.AccessToken = "test-only-application-token";
        options.RetryMinDelay = TimeSpan.FromMilliseconds(1);
        options.RetryMaxDelay = TimeSpan.FromMilliseconds(2);
    }

    /// <summary>完成首次读取握手，返回同一宿主构建器。</summary>
    /// <typeparam name="TBuilder">现代宿主构建器。</typeparam>
    /// <param name="builder">待接入的构建器。</param>
    /// <param name="fileId">远程文件身份。</param>
    /// <param name="json">首次配置正文。</param>
    /// <param name="version">首次发布版本。</param>
    /// <returns>完成接入的构建器。</returns>
    internal async Task<TBuilder> RegisterAsync<TBuilder>(TBuilder builder, Guid fileId, string json = "{\"Sample\":{\"Message\":\"first\"}}", long version = 1)
        where TBuilder : IHostApplicationBuilder
    {
        var pending = TrelixHostExtensions.AddCoreAsync(builder, Configure, this, TimeProvider.System, TestContext.Current.CancellationToken);
        var read = await NextAsync();
        Assert.EndsWith("/prefix/api/application/configuration", read.Uri.AbsolutePath);
        read.Publish(fileId, version, json);
        return await pending;
    }
}

/// <summary>测试可以显式完成的单次请求。</summary>
/// <param name="uri">请求目标。</param>
/// <param name="token">请求头中的测试凭证。</param>
internal sealed class Exchange(Uri uri, string? token)
{
    internal Uri Uri { get; } = uri;
    internal string? Token { get; } = token;
    internal TaskCompletionSource<HttpResponseMessage> Response { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>完成带发布内容的读取响应。</summary>
    /// <param name="fileId">文件身份。</param>
    /// <param name="version">版本。</param>
    /// <param name="json">配置正文。</param>
    internal void Publish(Guid fileId, long version, string json) => Json(new { configFileId = fileId, version, publishedAt = DateTimeOffset.UtcNow, json });

    /// <summary>完成监听变化响应。</summary>
    /// <param name="fileId">文件身份。</param>
    /// <param name="version">版本。</param>
    internal void Changed(Guid fileId, long version) => Json(new { configFileId = fileId, version });

    /// <summary>使用标准传输序列化输出测试响应。</summary>
    /// <param name="body">响应对象。</param>
    internal void Json(object body) => Reply(HttpStatusCode.OK, JsonSerializer.Serialize(body));

    /// <summary>完成指定状态的响应。</summary>
    /// <param name="status">HTTP 状态。</param>
    /// <param name="body">可选测试正文。</param>
    internal void Reply(HttpStatusCode status, string body = "") => Response.SetResult(new HttpResponseMessage(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    });
}
