namespace Trelix.Server.Persistence.Entities;

/// <summary>唯一内置管理员的密码哈希与会话安全戳。</summary>
public sealed class Administrator
{
    public int Id { get; set; } = 1;
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
}

/// <summary>持久化管理员会话，保存到期时间及签发时的安全戳。</summary>
public sealed class AdministratorSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int AdministratorId { get; set; } = 1;
    public Guid SecurityStamp { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>应用只读凭证的摘要、生命周期、授权范围与并发标记。</summary>
public sealed class ApplicationToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string SecretHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public List<TokenScope> Scopes { get; set; } = [];
}

/// <summary>将一个应用令牌授予一个环境的读取权限，项目归属由环境决定。</summary>
public sealed class TokenScope
{
    public Guid ApplicationTokenId { get; set; }
    public Guid EnvironmentId { get; set; }
}
