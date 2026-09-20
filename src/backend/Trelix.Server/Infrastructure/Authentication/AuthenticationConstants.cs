namespace Trelix.Server.Infrastructure.Authentication;

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
