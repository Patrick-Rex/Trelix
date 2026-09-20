using System.ComponentModel.DataAnnotations;

namespace Trelix.Server.Features.ApplicationTokens;

/// <summary>令牌允许访问的项目和环境，服务端核验实际归属。</summary>
public sealed record TokenScopeRequest(Guid ProjectId, Guid EnvironmentId);

/// <summary>创建只读应用令牌；有效期和至少一个明确授权范围必填。</summary>
public sealed record CreateApplicationTokenRequest
{
    [Required, MaxLength(200)]
    public required string Name { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
    [Required, MinLength(1), MaxLength(100)]
    public required TokenScopeRequest[] Scopes { get; init; }
}

/// <summary>轮换令牌，沿用原名称与授权范围，指定新有效期。</summary>
public sealed record RotateApplicationTokenRequest
{
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>令牌元数据，不包含原文和校验摘要。</summary>
public sealed record ApplicationTokenResponse(Guid Id, string Name, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, IReadOnlyList<TokenScopeRequest> Scopes);

/// <summary>只在创建或轮换成功时返回一次的新令牌原文。</summary>
public sealed record IssuedApplicationTokenResponse(ApplicationTokenResponse Token, string Secret);

/// <summary>按创建时间倒序返回的有界令牌列表。</summary>
public sealed record ApplicationTokenListResponse(IReadOnlyList<ApplicationTokenResponse> Items, int Page, int PageSize);
