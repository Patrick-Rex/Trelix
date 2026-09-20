using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trelix.Server.Persistence;

namespace Trelix.Server.Infrastructure.Authentication;

public sealed class ApplicationTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    TrelixDbContext db,
    TimeProvider time) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values))
            return AuthenticateResult.NoResult();
        if (values.Count != 1 || !AuthenticationHeaderValue.TryParse(values[0], out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase)
            || header.Parameter is not { } secret || !ApplicationTokenSecret.HasValidFormat(secret))
            return AuthenticateResult.Fail("Invalid application credential.");

        var hash = ApplicationTokenSecret.Hash(secret);
        var now = time.GetUtcNow();
        var id = await db.ApplicationTokens.Where(x => x.SecretHash == hash && x.RevokedAt == null && x.ExpiresAt > now)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(Context.RequestAborted);
        if (id is null)
            return AuthenticateResult.Fail("Invalid application credential.");

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.Value.ToString())], Scheme.Name);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
}
