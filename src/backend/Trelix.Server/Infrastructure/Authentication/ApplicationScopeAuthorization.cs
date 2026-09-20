using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence;

namespace Trelix.Server.Infrastructure.Authentication;

public sealed record ApplicationResource(Guid ProjectId, Guid EnvironmentId);
public sealed class ApplicationScopeRequirement : IAuthorizationRequirement;

// Distribution endpoints must use this resource requirement after ApplicationPolicy.
// Re-evaluate after long polling; claims never cache grants or lifecycle state.
public sealed class ApplicationScopeHandler(TrelixDbContext db, TimeProvider time, IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<ApplicationScopeRequirement, ApplicationResource>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ApplicationScopeRequirement requirement, ApplicationResource resource)
    {
        var identity = context.User.Identities.SingleOrDefault(x => x.AuthenticationType == AuthenticationConstants.ApplicationScheme);
        if (!Guid.TryParse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var tokenId))
            return;

        var now = time.GetUtcNow();
        var allowed = await (from scope in db.TokenScopes
                             join environment in db.Environments on scope.EnvironmentId equals environment.Id
                             join token in db.ApplicationTokens on scope.ApplicationTokenId equals token.Id
                             where token.Id == tokenId && token.RevokedAt == null && token.ExpiresAt > now
                                 && environment.Id == resource.EnvironmentId && environment.ProjectId == resource.ProjectId
                             select token.Id).AnyAsync(httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None);
        if (allowed)
            context.Succeed(requirement);
    }
}
