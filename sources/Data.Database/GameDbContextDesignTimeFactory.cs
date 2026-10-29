using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Data.Database;

/// <summary>
/// Used only by "dotnet ef" to create migrations. No database connection is opened for that,
/// so the connection string is a placeholder.
/// </summary>
public sealed class GameDbContextDesignTimeFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    public GameDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=localhost;Database=gamehub")
            .Options;

        return new GameDbContext(options);
    }
}
