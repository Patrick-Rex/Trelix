using System.ComponentModel.DataAnnotations;

namespace Trelix.Server.Features.Authentication;

/// <summary>管理员登录凭证。</summary>
public sealed record LoginRequest
{
    /// <summary>管理员账号名，按原值精确匹配。</summary>
    [Required, MaxLength(128)]
    public required string Username { get; init; }
    /// <summary>待校验的管理员密码，仅用于本次登录。</summary>
    [Required, MaxLength(1024)]
    public required string Password { get; init; }
}

/// <summary>当前管理员会话，不包含 Cookie 或内部校验信息。</summary>
/// <param name="Username">管理员账号名。</param>
/// <param name="ExpiresAt">到期时间。</param>
public sealed record SessionResponse(string Username, DateTimeOffset ExpiresAt);

/// <summary>管理写请求使用的防伪造请求令牌及请求头名称。</summary>
/// <param name="RequestToken">与当前身份绑定的防伪造请求令牌。</param>
/// <param name="HeaderName">客户端传递请求令牌的请求头名称。</param>
public sealed record AntiforgeryResponse(string RequestToken, string HeaderName);
