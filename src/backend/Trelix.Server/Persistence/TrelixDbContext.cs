using Microsoft.EntityFrameworkCore;
using Trelix.Server.Persistence.Entities;

namespace Trelix.Server.Persistence;

public sealed class TrelixDbContext(DbContextOptions<TrelixDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectEnvironment> Environments => Set<ProjectEnvironment>();
    public DbSet<ConfigFile> ConfigFiles => Set<ConfigFile>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<Administrator> Administrators => Set<Administrator>();
    public DbSet<AdministratorSession> AdministratorSessions => Set<AdministratorSession>();
    public DbSet<ApplicationToken> ApplicationTokens => Set<ApplicationToken>();
    public DbSet<TokenScope> TokenScopes => Set<TokenScope>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TrelixDbContext).Assembly);
    }

    private void PrepareChanges()
    {
        foreach (var entry in ChangeTracker.Entries<Release>())
        {
            if (entry.State == EntityState.Modified)
                throw new InvalidOperationException("Published releases are immutable.");
        }

        foreach (var entry in ChangeTracker.Entries<ConfigFile>())
            if (entry.State == EntityState.Modified)
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();

        foreach (var entry in ChangeTracker.Entries<ApplicationToken>())
            if (entry.State == EntityState.Modified)
                entry.Entity.ConcurrencyStamp = Guid.NewGuid();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        PrepareChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        PrepareChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
