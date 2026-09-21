namespace Trelix.Server.Infrastructure;

// Only explicitly authored, safe public messages belong here; never wrap exception.Message.
/// <summary>携带可公开的状态码、业务错误码和提示；不得包装原始异常正文。</summary>
/// <param name="status">HTTP 响应状态码。</param>
/// <param name="code">客户端可识别的业务错误码。</param>
/// <param name="title">允许公开的错误提示。</param>
public sealed class ApiOperationException(int status, string code, string title) : Exception(title)
{
    /// <summary>错误响应使用的 HTTP 状态码。</summary>
    public int Status { get; } = status;
    /// <summary>供客户端识别错误类别的稳定业务错误码。</summary>
    public string Code { get; } = code;
}
