using System.ComponentModel.DataAnnotations;

namespace Trelix.Server.Features.Authentication;

/// <summary>管理员登录凭证。</summary>
public sealed record LoginRequest
{
    [Required, MaxLength(128)]
    public required string Username { get; init; }
    [Required, MaxLength(1024)]
    public required string Password { get; init; }
}

/// <summary>当前管理员会话，不包含 Cookie 或内部校验信息。</summary>
public sealed record SessionResponse(string Username, DateTimeOffset ExpiresAt);

/// <summary>管理写请求使用的防伪造请求令牌及请求头名称。</summary>
public sealed record AntiforgeryResponse(string RequestToken, string HeaderName);
