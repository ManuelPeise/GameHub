using Data.Database.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Data.Database;

/// <summary>
/// The GameHub database, including the ASP.NET Core Identity tables (schema "identity").
/// Schema changes only through EF Core migrations (Data.Database/Migrations), applied by
/// Services.DbTools ("migrate"), never at application start.
/// </summary>
public sealed class GameDbContext(DbContextOptions<GameDbContext> options)
    : IdentityDbContext<User, Role, Guid>(options)
{
    public const string LogSchema = "log";
    public const string AuditSchema = "audit";
    public const string IdentitySchema = "identity";
    public const string GameSchema = "game";

    public DbSet<AppLogEntry> AppLog => Set<AppLogEntry>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<GameSession> GameSessions => Set<GameSession>();

    public DbSet<Seat> Seats => Set<Seat>();

    public DbSet<GameAction> GameActions => Set<GameAction>();

    public DbSet<GameResult> GameResults => Set<GameResult>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Move log and audit log are append-only: changing or deleting an entry is a programming error.</summary>
    private void EnsureAppendOnly()
    {
        var violation = ChangeTracker.Entries()
            .FirstOrDefault(entry =>
                entry.Entity is GameAction or AuditEntry
                && entry.State is EntityState.Modified or EntityState.Deleted);

        if (violation is not null)
        {
            throw new InvalidOperationException(
                $"{violation.Entity.GetType().Name} is append-only and must not be {violation.State.ToString().ToLowerInvariant()}.");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Identity tables in their own schema with short names.
        modelBuilder.Entity<User>().ToTable("Users", IdentitySchema);
        modelBuilder.Entity<Role>().ToTable("Roles", IdentitySchema);
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", IdentitySchema);
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", IdentitySchema);
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", IdentitySchema);
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", IdentitySchema);
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", IdentitySchema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
