using System.Net;

namespace Trelix.Extensions.Configuration;

/// <summary>不保留响应正文、地址、凭证或底层异常文本的 SDK 故障。</summary>
public sealed class TrelixConfigurationException : Exception
{
    /// <summary>创建带稳定类别的安全诊断异常。</summary>
    /// <param name="code">稳定故障类别。</param>
    /// <param name="statusCode">可选 HTTP 状态。</param>
    public TrelixConfigurationException(string code, HttpStatusCode? statusCode = null)
        : base($"Trelix configuration operation failed ({code}).")
    {
        Code = code;
        StatusCode = statusCode;
    }

    /// <summary>稳定故障类别，不包含远程错误正文。</summary>
    public string Code { get; }
    /// <summary>服务端返回的 HTTP 状态，传输或解析故障时为空。</summary>
    public HttpStatusCode? StatusCode { get; }
}
