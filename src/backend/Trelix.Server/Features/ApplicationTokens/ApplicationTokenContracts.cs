using System.ComponentModel.DataAnnotations;

namespace Trelix.Server.Features.ApplicationTokens;

/// <summary>令牌允许访问的项目和环境，服务端核验实际归属。</summary>
/// <param name="ProjectId">目标项目标识。</param>
/// <param name="EnvironmentId">目标环境标识。</param>
public sealed record TokenScopeRequest(Guid ProjectId, Guid EnvironmentId);

/// <summary>创建只读应用令牌；有效期和至少一个明确授权范围必填。</summary>
public sealed record CreateApplicationTokenRequest
{
    /// <summary>令牌显示名称，保存前去除首尾空白。</summary>
    [Required, MaxLength(200)]
    public required string Name { get; init; }
    /// <summary>必须晚于当前时间的到期时间。</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
    /// <summary>允许访问的项目环境集合，不得重复授权同一环境。</summary>
    [Required, MinLength(1), MaxLength(100)]
    public required TokenScopeRequest[] Scopes { get; init; }
}

/// <summary>轮换令牌，沿用原名称与授权范围，指定新有效期。</summary>
public sealed record RotateApplicationTokenRequest
{
    /// <summary>新令牌的到期时间，必须晚于当前时间。</summary>
    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>令牌元数据，不包含原文和校验摘要。</summary>
/// <param name="Id">目标应用令牌标识。</param>
/// <param name="Name">令牌显示名称。</param>
/// <param name="CreatedAt">令牌创建时间。</param>
/// <param name="ExpiresAt">到期时间。</param>
/// <param name="RevokedAt">撤销时间；尚未撤销时为 null。</param>
/// <param name="Scopes">令牌允许访问的项目环境集合。</param>
public sealed record ApplicationTokenResponse(Guid Id, string Name, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, DateTimeOffset? RevokedAt, IReadOnlyList<TokenScopeRequest> Scopes);

/// <summary>只在创建或轮换成功时返回一次的新令牌原文。</summary>
/// <param name="Token">新签发的令牌元数据。</param>
/// <param name="Secret">应用凭证原文。</param>
public sealed record IssuedApplicationTokenResponse(ApplicationTokenResponse Token, string Secret);

/// <summary>按创建时间倒序返回的有界令牌列表。</summary>
/// <param name="Items">当前页的令牌元数据。</param>
/// <param name="Page">从 1 开始的页码。</param>
/// <param name="PageSize">每页返回的最大条目数。</param>
public sealed record ApplicationTokenListResponse(IReadOnlyList<ApplicationTokenResponse> Items, int Page, int PageSize);
