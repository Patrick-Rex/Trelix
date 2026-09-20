using Microsoft.EntityFrameworkCore;
using Trelix.Server.Infrastructure;
using Trelix.Server.Infrastructure.Authentication;
using Trelix.Server.Persistence;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Features.ApplicationTokens;

public sealed class ApplicationTokenService(TrelixDbContext db, TimeProvider time)
{
    public async Task<ApplicationTokenListResponse> ListAsync(int page, int pageSize, CancellationToken cancellationToken)
    {
        var tokens = await ProjectTokens(db.ApplicationTokens.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(cancellationToken);
        return new ApplicationTokenListResponse(tokens, page, pageSize);
    }

    public Task<ApplicationTokenResponse?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        ProjectTokens(db.ApplicationTokens.Where(x => x.Id == id)).SingleOrDefaultAsync(cancellationToken);

    private IQueryable<ApplicationTokenResponse> ProjectTokens(IQueryable<ApplicationToken> source) => source.AsNoTracking().Select(token =>
        new ApplicationTokenResponse(token.Id, token.Name, token.CreatedAt, token.ExpiresAt, token.RevokedAt,
            (from scope in db.TokenScopes
             join environment in db.Environments on scope.EnvironmentId equals environment.Id
             where scope.ApplicationTokenId == token.Id
             orderby environment.ProjectId, environment.Id
             select new TokenScopeRequest(environment.ProjectId, environment.Id)).ToList()));

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

    public async Task RevokeAsync(Guid id, CancellationToken cancellationToken)
    {
        var token = await db.ApplicationTokens.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new ApiOperationException(404, "token_not_found", "令牌不存在。");
        if (token.RevokedAt is not null)
            return;
        token.RevokedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

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

    private static void ValidateExpiry(DateTimeOffset expiry, DateTimeOffset now)
    {
        if (expiry <= now)
            throw new ApiOperationException(400, "invalid_expiry", "有效期必须晚于当前时间。");
    }
}
