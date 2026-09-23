using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Trelix.Extensions.Configuration;

/// <summary>拥有整个接入生命周期的 HTTP 连接池，逐请求限制等待时间和响应大小。</summary>
internal sealed class TrelixHttpClient : IDisposable
{
    private readonly HttpClient client;
    private readonly TrelixOptions options;
    private readonly string query;

    /// <summary>建立固定连接目标，不使用宿主的默认重试管线。</summary>
    /// <param name="options">已经复制并校验的本地选项。</param>
    /// <param name="handler">内部测试传输；省略时使用独立连接池。</param>
    internal TrelixHttpClient(TrelixOptions options, HttpMessageHandler? handler = null)
    {
        this.options = options;
        client = new HttpClient(handler ?? new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2), AllowAutoRedirect = false, UseCookies = false
        }) { BaseAddress = new Uri(options.ServerUrl), Timeout = Timeout.InfiniteTimeSpan };
        query = "?projectKey=" + Uri.EscapeDataString(options.ProjectKey)
            + "&environmentKey=" + Uri.EscapeDataString(options.EnvironmentKey) + "&fileName=" + Uri.EscapeDataString(options.FileName);
    }

    /// <summary>读取并校验同一发布记录的身份与正文。</summary>
    /// <param name="ct">调用方取消信号。</param>
    /// <returns>完整有效快照。</returns>
    internal async Task<ConfigurationSnapshot> ReadAsync(CancellationToken ct)
    {
        using var document = await SendAsync("api/application/configuration" + query, options.ReadTimeout, 8 * 1024 * 1024, false, ct).ConfigureAwait(false);
        try
        {
            var root = document!.RootElement;
            var publishedAt = root.GetProperty("publishedAt").GetDateTimeOffset();
            if (publishedAt == default)
                throw new TrelixConfigurationException("invalid_response");
            return ConfigurationSnapshot.Parse(root.GetProperty("configFileId").GetGuid(), root.GetProperty("version").GetInt64(),
                root.GetProperty("json").GetString());
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new TrelixConfigurationException("invalid_response");
        }
    }

    /// <summary>监听当前身份的变化，只验证通知身份，不将通知元数据拼入后续读取结果。</summary>
    /// <param name="current">最近已应用的快照。</param>
    /// <param name="ct">调用方取消信号。</param>
    /// <returns>是否需要重新读取正文。</returns>
    internal async Task<bool> WatchAsync(ConfigurationSnapshot current, CancellationToken ct)
    {
        using var document = await SendAsync("api/application/configuration/watch" + query + "&knownConfigFileId=" + current.FileId
            + "&knownVersion=" + current.Version.ToString(CultureInfo.InvariantCulture), options.WatchTimeout, 16 * 1024, true, ct).ConfigureAwait(false);
        if (document is null)
            return false;
        try
        {
            var id = document.RootElement.GetProperty("configFileId").GetGuid();
            var version = document.RootElement.GetProperty("version").GetInt64();
            if (id == Guid.Empty || version <= 0 || (id == current.FileId && version <= current.Version))
                throw new TrelixConfigurationException("invalid_response");
            return true;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or InvalidOperationException or FormatException)
        {
            throw new TrelixConfigurationException("invalid_response");
        }
    }

    /// <summary>执行一个有界请求；错误仅公开状态与固定类别，不读取错误正文。</summary>
    /// <param name="path">编码后的相对路径。</param>
    /// <param name="timeout">包括正文读取在内的总超时。</param>
    /// <param name="limit">最大响应字节数。</param>
    /// <param name="allowNoContent">是否允许正常的 204 响应。</param>
    /// <param name="ct">调用方取消信号。</param>
    /// <returns>解析后的传输外壳；204 返回 null。</returns>
    private async Task<JsonDocument?> SendAsync(string path, TimeSpan timeout, int limit, bool allowNoContent, CancellationToken ct)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(timeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.AccessToken);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
            if (allowNoContent && response.StatusCode == HttpStatusCode.NoContent)
                return null;
            if (response.StatusCode != HttpStatusCode.OK)
                throw new TrelixConfigurationException("http_error", response.StatusCode);
            if (response.Content.Headers.ContentLength > limit)
                throw new TrelixConfigurationException("response_too_large");
            await using var stream = await response.Content.ReadAsStreamAsync(deadline.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            while (true)
            {
                var read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit + 1L - buffer.Length)), deadline.Token).ConfigureAwait(false);
                if (read == 0)
                    break;
                buffer.Write(chunk, 0, read);
                if (buffer.Length > limit)
                    throw new TrelixConfigurationException("response_too_large");
            }
            return JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, (int)buffer.Length), new JsonDocumentOptions { MaxDepth = 8, AllowDuplicateProperties = false });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TrelixConfigurationException("request_timeout");
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            throw new TrelixConfigurationException("transport_error");
        }
        catch (JsonException)
        {
            throw new TrelixConfigurationException("invalid_response");
        }
    }

    /// <summary>释放接入拥有的客户端和连接池。</summary>
    public void Dispose() => client.Dispose();
}
