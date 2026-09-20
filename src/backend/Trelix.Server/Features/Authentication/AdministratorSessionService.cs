using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Features.Authentication;

public sealed class AdministratorSessionService(TrelixDbContext db, IPasswordHasher<Administrator> passwords, TimeProvider time)
{
    public async Task<SessionResponse?> LoginAsync(HttpContext context, LoginRequest request, CancellationToken cancellationToken)
    {
        var admin = await db.Administrators.SingleAsync(cancellationToken);
        // Always verify the hash, including when the username does not match.
        var result = passwords.VerifyHashedPassword(admin, admin.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed || !string.Equals(admin.Username, request.Username, StringComparison.Ordinal))
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            admin.PasswordHash = passwords.HashPassword(admin, request.Password);

        var now = time.GetUtcNow();
        var previousId = Guid.TryParse(context.User.FindFirstValue(AuthenticationConstants.SessionClaim), out var id) ? id : Guid.Empty;
        await db.AdministratorSessions.Where(x => x.ExpiresAt <= now || x.Id == previousId).ExecuteDeleteAsync(cancellationToken);
        var session = new AdministratorSession { SecurityStamp = admin.SecurityStamp, ExpiresAt = now + AuthenticationConstants.SessionLifetime };
        db.AdministratorSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        var identity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "1"),
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(AuthenticationConstants.SessionClaim, session.Id.ToString())
        ], AuthenticationConstants.AdministratorScheme);

        await context.SignInAsync(AuthenticationConstants.AdministratorScheme, new ClaimsPrincipal(identity), new AuthenticationProperties
        {
            IssuedUtc = now,
            ExpiresUtc = session.ExpiresAt,
            IsPersistent = false,
            AllowRefresh = false
        });
        return new SessionResponse(admin.Username, session.ExpiresAt);
    }

    public async Task<SessionResponse?> GetAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var sessionId = Guid.Parse(user.FindFirstValue(AuthenticationConstants.SessionClaim)!);
        var now = time.GetUtcNow();
        var expiry = await db.AdministratorSessions.Where(x => x.Id == sessionId && x.ExpiresAt > now)
            .Select(x => (DateTimeOffset?)x.ExpiresAt).SingleOrDefaultAsync(cancellationToken);
        return expiry is null ? null : new SessionResponse(user.Identity!.Name!, expiry.Value);
    }

    public async Task LogoutAsync(HttpContext context, CancellationToken cancellationToken)
    {
        var sessionId = Guid.Parse(context.User.FindFirstValue(AuthenticationConstants.SessionClaim)!);
        await db.AdministratorSessions.Where(x => x.Id == sessionId).ExecuteDeleteAsync(cancellationToken);
        await context.SignOutAsync(AuthenticationConstants.AdministratorScheme);
    }
}
