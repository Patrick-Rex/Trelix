using Trelix.Server.Infrastructure.Persistence.Entities;

namespace Trelix.Server.Infrastructure.Authentication;

/// <summary>复用应用令牌的数据库生命周期筛选规则。</summary>
internal static class ApplicationTokenQueryExtensions
{
    /// <summary>筛选在指定时间尚未撤销且尚未过期的令牌。</summary>
    /// <param name="source">待筛选的令牌查询。</param>
    /// <param name="now">调用方获取的当前时间。</param>
    /// <returns>延迟执行的有效令牌查询。</returns>
    internal static IQueryable<ApplicationToken> WhereActiveAt(this IQueryable<ApplicationToken> source, DateTimeOffset now) =>
        source.Where(x => x.RevokedAt == null && x.ExpiresAt > now);
}
