namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>集中维护管理员与应用认证方案、策略名称及会话约定。</summary>
public static class AuthenticationConstants
{
    public const string AdministratorScheme = "AdministratorCookie";
    public const string ApplicationScheme = "ApplicationToken";
    public const string AdministratorPolicy = "Administrator";
    public const string ApplicationPolicy = "Application";
    public const string SessionClaim = "trelix_session";
    public const string CsrfHeader = "X-Trelix-CSRF";
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);
}
