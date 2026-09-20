using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence;

namespace Trelix.Server.Infrastructure.Authentication;

public sealed class AdministratorCookieEvents(TrelixDbContext db, TimeProvider time) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var now = time.GetUtcNow();
        if (!Guid.TryParse(context.Principal?.FindFirstValue(AuthenticationConstants.SessionClaim), out var sessionId)
            || context.Principal.FindFirstValue(ClaimTypes.NameIdentifier) != "1"
            || !await (from session in db.AdministratorSessions
                       join admin in db.Administrators on session.AdministratorId equals admin.Id
                       where session.Id == sessionId && session.ExpiresAt > now && session.SecurityStamp == admin.SecurityStamp
                       select session.Id).AnyAsync(context.HttpContext.RequestAborted))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(AuthenticationConstants.AdministratorScheme);
        }
    }

    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
