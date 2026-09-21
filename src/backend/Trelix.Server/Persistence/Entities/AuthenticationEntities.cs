namespace Trelix.Server.Persistence.Entities;

/// <summary>唯一内置管理员的密码哈希与会话安全戳。</summary>
public sealed class Administrator
{
    /// <summary>唯一管理员的固定标识，数据库约束为 1。</summary>
    public int Id { get; set; } = 1;
    /// <summary>登录时按原值精确匹配的账号名。</summary>
    public required string Username { get; set; }
    /// <summary>由密码哈希器生成的密码校验值，不保存密码原文。</summary>
    public required string PasswordHash { get; set; }
    /// <summary>会话校验及管理员并发保护使用的安全戳。</summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
}

/// <summary>持久化管理员会话，保存到期时间及签发时的安全戳。</summary>
public sealed class AdministratorSession
{
    /// <summary>写入登录身份声明的会话标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>会话所属管理员的标识。</summary>
    public int AdministratorId { get; set; } = 1;
    /// <summary>会话建立时的管理员安全戳，变化后会话失效。</summary>
    public Guid SecurityStamp { get; set; }
    /// <summary>会话的固定到期时间。</summary>
    public DateTimeOffset ExpiresAt { get; set; }
}

/// <summary>应用只读凭证的摘要、生命周期、授权范围与并发标记。</summary>
public sealed class ApplicationToken
{
    /// <summary>令牌元数据与授权关联使用的标识。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>供管理员辨识用途的令牌名称。</summary>
    public required string Name { get; set; }
    /// <summary>凭证原文的 SHA-256 十六进制摘要，不保存原文。</summary>
    public required string SecretHash { get; set; }
    /// <summary>令牌签发时间。</summary>
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>令牌到期时间，必须晚于创建时间。</summary>
    public DateTimeOffset ExpiresAt { get; set; }
    /// <summary>撤销时间；为 null 时仍须结合到期时间判断有效性。</summary>
    public DateTimeOffset? RevokedAt { get; set; }
    /// <summary>修改令牌时刷新的乐观并发标记。</summary>
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    /// <summary>该令牌获准读取的环境集合。</summary>
    public List<TokenScope> Scopes { get; set; } = [];
}

/// <summary>将一个应用令牌授予一个环境的读取权限，项目归属由环境决定。</summary>
public sealed class TokenScope
{
    /// <summary>被授予访问权限的应用令牌标识。</summary>
    public Guid ApplicationTokenId { get; set; }
    /// <summary>获授权环境的标识，其项目归属由环境实体确定。</summary>
    public Guid EnvironmentId { get; set; }
}
