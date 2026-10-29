using Data.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace Data.Database;

/// <summary>
/// The GameHub database. Tables for sessions, users and the audit log follow with their user stories.
/// Schema changes only through EF Core migrations (Data.Database/Migrations), applied by
/// Services.DbTools ("migrate"), never at application start.
/// </summary>
public sealed class GameDbContext(DbContextOptions<GameDbContext> options) : DbContext(options)
{
    public const string LogSchema = "log";

    public DbSet<AppLogEntry> AppLog => Set<AppLogEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameDbContext).Assembly);
    }
}
