namespace Trelix.Extensions.Configuration;

/// <summary>从本地 Trelix 节绑定的连接与重试参数；接入后固定，不受远程配置影响。</summary>
public sealed class TrelixOptions
{
    /// <summary>不含凭证、查询或片段的 HTTP/HTTPS 服务根地址，可包含路径前缀。</summary>
    public string ServerUrl { get; set; } = "";
    /// <summary>区分大小写的项目业务标识。</summary>
    public string ProjectKey { get; set; } = "";
    /// <summary>区分大小写的项目内环境业务标识。</summary>
    public string EnvironmentKey { get; set; } = "";
    /// <summary>区分大小写的配置文件原名。</summary>
    public string FileName { get; set; } = "";
    /// <summary>外部注入的应用只读令牌，绝不从远程配置获取。</summary>
    public string AccessToken { get; set; } = "";
    /// <summary>读取请求及响应正文的总超时。</summary>
    public TimeSpan ReadTimeout { get; set; } = TimeSpan.FromSeconds(10);
    /// <summary>监听请求总超时，必须大于服务端等待时间并保留传输余量。</summary>
    public TimeSpan WatchTimeout { get; set; } = TimeSpan.FromSeconds(40);
    /// <summary>运行期失败重试的最短间隔。</summary>
    public TimeSpan RetryMinDelay { get; set; } = TimeSpan.FromSeconds(1);
    /// <summary>运行期失败重试的最长间隔。</summary>
    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>复制并验证选项，防止调用者后续修改同一实例改变连接目标。</summary>
    /// <returns>本次接入拥有的连接选项副本。</returns>
    internal TrelixOptions Freeze()
    {
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0
            || !ValidName(ProjectKey, 128) || !ValidName(EnvironmentKey, 128) || !ValidName(FileName, 256)
            || string.IsNullOrWhiteSpace(AccessToken) || AccessToken.Length > 1024
            || AccessToken.Contains('<') || AccessToken.Any(character => character <= ' ' || character >= '\u007f')
            || !ValidDuration(ReadTimeout) || !ValidDuration(WatchTimeout)
            || !ValidDuration(RetryMinDelay) || !ValidDuration(RetryMaxDelay) || RetryMaxDelay < RetryMinDelay)
            throw new TrelixConfigurationException("invalid_options");
        return new()
        {
            ServerUrl = uri.AbsoluteUri.TrimEnd('/') + "/", ProjectKey = ProjectKey, EnvironmentKey = EnvironmentKey,
            FileName = FileName, AccessToken = AccessToken, ReadTimeout = ReadTimeout, WatchTimeout = WatchTimeout,
            RetryMinDelay = RetryMinDelay, RetryMaxDelay = RetryMaxDelay
        };
    }

    /// <summary>验证名称符合服务端长度与非空白限制，保留原值。</summary>
    /// <param name="value">名称。</param>
    /// <param name="maximum">允许的最大字符数。</param>
    /// <returns>名称是否有效。</returns>
    private static bool ValidName(string value, int maximum) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;

    /// <summary>保证超时和定时器间隔有界。</summary>
    /// <param name="value">待验证间隔。</param>
    /// <returns>是否位于零到一天之间。</returns>
    private static bool ValidDuration(TimeSpan value) => value > TimeSpan.Zero && value <= TimeSpan.FromDays(1);
}
