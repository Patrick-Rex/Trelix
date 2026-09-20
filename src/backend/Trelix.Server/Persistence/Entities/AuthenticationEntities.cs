namespace Trelix.Server.Persistence.Entities;

public sealed class Administrator
{
    public int Id { get; set; } = 1;
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();
}

public sealed class AdministratorSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int AdministratorId { get; set; } = 1;
    public Guid SecurityStamp { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

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

public sealed class TokenScope
{
    public Guid ApplicationTokenId { get; set; }
    public Guid EnvironmentId { get; set; }
}
