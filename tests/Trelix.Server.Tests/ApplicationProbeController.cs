using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Trelix.Server.Infrastructure.Authentication;

namespace Trelix.Server.Tests;

// Registered only by ServerFactory. These routes prove M2's schemes and resource policy,
// and do not stand in for the M3/M5 production distribution endpoints.
[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = AuthenticationConstants.ApplicationPolicy)]
[Route("__tests/application")]
public sealed class ApplicationProbeController(IAuthorizationService authorization) : ControllerBase
{
    [HttpGet("{projectId:guid}/{environmentId:guid}")]
    public async Task<IActionResult> Resource(Guid projectId, Guid environmentId)
    {
        var result = await authorization.AuthorizeAsync(User, new ApplicationResource(projectId, environmentId), new ApplicationScopeRequirement());
        return result.Succeeded ? NoContent() : Forbid(AuthenticationConstants.ApplicationScheme);
    }
}

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Authorize(Policy = AuthenticationConstants.AdministratorPolicy)]
[Route("api/admin/__tests")]
public sealed class ManagementProbeController : ControllerBase
{
    [HttpGet("failure")]
    public IActionResult Failure() => throw new InvalidOperationException("sensitive-internal-body");
}
