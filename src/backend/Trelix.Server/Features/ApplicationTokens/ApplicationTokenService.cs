using Microsoft.EntityFrameworkCore;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Features.ApplicationTokens;

/// <summary>负责应用令牌签发、查询、撤销和原子轮换。</summary>
/// <param name="db">当前作用域的数据库上下文。</param>
/// <param name="time">用于生命周期校验的时间提供程序。</param>
public sealed class ApplicationTokenService(TrelixDbContext db, TimeProvider time)
{
    /// <summary>按创建时间倒序和标识分页查询令牌元数据。</summary>
    /// <param name="page">从 1 开始的页码。</param>
    /// <param name="pageSize">每页返回的最大条目数。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>有界的令牌列表及分页信息。</returns>
    public async Task<ApplicationTokenListResponse> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var tokens = await ProjectTokens(db.ApplicationTokens.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(cancellationToken);
        return new ApplicationTokenListResponse(tokens, page, pageSize);
    }

    /// <summary>根据标识查询令牌元数据及授权范围。</summary>
    /// <param name="id">目标应用令牌标识。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>令牌元数据；不存在时为 null。</returns>
    public Task<ApplicationTokenResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ProjectTokens(db.ApplicationTokens.Where(x => x.Id == id)).SingleOrDefaultAsync(cancellationToken);

    /// <summary>将只读令牌查询投影为不包含凭证或摘要的响应契约。</summary>
    /// <param name="source">待投影的令牌查询。</param>
    /// <returns>包含授权范围的延迟执行查询。</returns>
    private IQueryable<ApplicationTokenResponse> ProjectTokens(IQueryable<ApplicationToken> source) => source.AsNoTracking().Select(token =>
        new ApplicationTokenResponse(token.Id, token.Name, token.CreatedAt, token.ExpiresAt, token.RevokedAt,
            (from scope in db.TokenScopes
             join environment in db.Environments on scope.EnvironmentId equals environment.Id
             where scope.ApplicationTokenId == token.Id
             orderby environment.ProjectId, environment.Id
             select new TokenScopeRequest(environment.ProjectId, environment.Id)).ToList()));

    /// <summary>校验有效期与范围归属，保存凭证摘要并返回一次性原文。</summary>
    /// <param name="request">包含名称、有效期和授权范围的创建请求。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>签发的令牌元数据与原文。</returns>
    public async Task<IssuedApplicationTokenResponse> CreateAsync(CreateApplicationTokenRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        ValidateExpiry(request.ExpiresAt, now);
        if (string.IsNullOrWhiteSpace(request.Name) || request.Scopes.Any(x => x is null || x.ProjectId == Guid.Empty || x.EnvironmentId == Guid.Empty)
            || request.Scopes.Select(x => x.EnvironmentId).Distinct().Count() != request.Scopes.Length)
            throw new ApiOperationException(400, "invalid_token", "令牌名称及授权范围无效或重复。");

        var environmentIds = request.Scopes.Select(x => x.EnvironmentId).ToArray();
        var environments = await db.Environments.Where(x => environmentIds.Contains(x.Id))
            .Select(x => new { x.Id, x.ProjectId }).ToListAsync(cancellationToken);
        if (request.Scopes.Any(scope => !environments.Any(x => x.Id == scope.EnvironmentId && x.ProjectId == scope.ProjectId)))
            throw new ApiOperationException(400, "invalid_scope", "授权环境不存在或不属于指定项目。");

        var (token, secret) = CreateToken(request.Name.Trim(), request.ExpiresAt, now, environmentIds);
        db.ApplicationTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
        return new IssuedApplicationTokenResponse((await GetAsync(token.Id, cancellationToken))!, secret);
    }

    /// <summary>标记令牌撤销时间；已撤销时直接返回。</summary>
    /// <param name="id">目标应用令牌标识。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>表示撤销完成的任务。</returns>
    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var token = await db.ApplicationTokens.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiOperationException(404, "token_not_found", "令牌不存在。");
        if (token.RevokedAt is not null)
            return;
        token.RevokedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>原子撤销旧令牌并保存继承名称与授权范围的新令牌。</summary>
    /// <param name="id">目标应用令牌标识。</param>
    /// <param name="request">指定新令牌到期时间的轮换请求。</param>
    /// <param name="cancellationToken">取消当前操作的令牌。</param>
    /// <returns>新令牌元数据与一次性原文。</returns>
    public async Task<IssuedApplicationTokenResponse> RotateAsync(Guid id, RotateApplicationTokenRequest request, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        ValidateExpiry(request.ExpiresAt, now);
        var original = await db.ApplicationTokens.Include(x => x.Scopes).SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiOperationException(404, "token_not_found", "令牌不存在。");
        if (original.RevokedAt is not null)
            throw new ApiOperationException(409, "token_revoked", "已撤销的令牌不能轮换，请重新创建。");

        var (replacement, secret) = CreateToken(original.Name, request.ExpiresAt, now, original.Scopes.Select(x => x.EnvironmentId));
        original.RevokedAt = now;
        db.ApplicationTokens.Add(replacement);
        // A single SaveChanges transaction revokes the original and inserts the replacement.
        // Optimistic concurrency prevents concurrent rotation/revocation from issuing two successors.
        await db.SaveChangesAsync(cancellationToken);
        return new IssuedApplicationTokenResponse((await GetAsync(replacement.Id, cancellationToken))!, secret);
    }

    /// <summary>生成随机凭证及待持久化的令牌实体，不执行数据库写入。</summary>
    /// <param name="name">令牌显示名称。</param>
    /// <param name="expiry">待校验或设置的到期时间。</param>
    /// <param name="now">当前 UTC 时间。</param>
    /// <param name="environments">授予访问权限的环境标识集合。</param>
    /// <returns>保存摘要的实体与仅供签发响应使用的原文。</returns>
    private static (ApplicationToken Token, string Secret) CreateToken(string name, DateTimeOffset expiry,
        DateTimeOffset now, IEnumerable<Guid> environments)
    {
        var secret = ApplicationTokenSecret.Create();
        var token = new ApplicationToken
        {
            Name = name, SecretHash = ApplicationTokenSecret.Hash(secret), CreatedAt = now, ExpiresAt = expiry,
            Scopes = environments.Select(id => new TokenScope { EnvironmentId = id }).ToList()
        };
        return (token, secret);
    }

    /// <summary>校验有效期严格晚于当前时间，否则抛出业务校验异常。</summary>
    /// <param name="expiry">待校验或设置的到期时间。</param>
    /// <param name="now">当前 UTC 时间。</param>
    private static void ValidateExpiry(DateTimeOffset expiry, DateTimeOffset now)
    {
        if (expiry <= now)
            throw new ApiOperationException(400, "invalid_expiry", "有效期必须晚于当前时间。");
    }
}
