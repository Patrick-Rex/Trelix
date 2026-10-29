using System.Security.Claims;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>从认证主体中解析应用令牌身份。</summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>从唯一的应用身份中解析令牌标识，不检查其生命周期或授权。</summary>
    /// <param name="user">包含零个或一个应用身份的主体。</param>
    /// <param name="tokenId">成功解析的令牌标识。</param>
    /// <returns>标识声明存在且可解析时为 true。</returns>
    /// <exception cref="InvalidOperationException">主体包含多个应用身份。</exception>
    internal static bool TryGetApplicationTokenId(this ClaimsPrincipal user, out Guid tokenId)
    {
        var identity = user.Identities.SingleOrDefault(x => x.AuthenticationType == AuthenticationConstants.ApplicationScheme);
        return Guid.TryParse(identity?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out tokenId);
    }
}
