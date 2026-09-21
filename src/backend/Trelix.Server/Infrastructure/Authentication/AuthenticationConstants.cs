namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>集中维护管理员与应用认证方案、策略名称及会话约定。</summary>
public static class AuthenticationConstants
{
    /// <summary>管理员 Cookie 身份验证方案名称。</summary>
    public const string AdministratorScheme = "AdministratorCookie";
    /// <summary>应用只读令牌身份验证方案名称。</summary>
    public const string ApplicationScheme = "ApplicationToken";
    /// <summary>仅允许已认证管理员访问的授权策略名称。</summary>
    public const string AdministratorPolicy = "Administrator";
    /// <summary>要求应用令牌身份的授权策略名称，资源范围另行校验。</summary>
    public const string ApplicationPolicy = "Application";
    /// <summary>存放持久化管理员会话标识的声明类型。</summary>
    public const string SessionClaim = "trelix_session";
    /// <summary>管理写请求携带防伪造令牌的请求头名称。</summary>
    public const string CsrfHeader = "X-Trelix-CSRF";
    /// <summary>管理员会话的固定有效时长，不滑动续期。</summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
}
