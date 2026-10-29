using System.Security.Claims;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>复用应用令牌身份解析及数据库中的生命周期筛选规则。</summary>
internal static class ApplicationTokenExtensions
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

    /// <summary>筛选在指定时间尚未撤销且尚未过期的令牌。</summary>
    /// <param name="source">待筛选的令牌查询。</param>
    /// <param name="now">调用方获取的当前时间。</param>
    /// <returns>延迟执行的有效令牌查询。</returns>
    internal static IQueryable<ApplicationToken> WhereActiveAt(this IQueryable<ApplicationToken> source, DateTimeOffset now) =>
        source.Where(x => x.RevokedAt == null && x.ExpiresAt > now);
}
