using Microsoft.Extensions.Hosting;

namespace Data.Database;

public static class DatabaseRegistration
{
    /// <summary>Name of the database resource in Services.AppHost and of the connection string.</summary>
    public const string ConnectionName = "gamehub";

    /// <summary>
    /// Registers <see cref="GameDbContext"/> with the connection string "gamehub"
    /// (injected by Aspire, in Docker by an environment variable) including health check and retries.
    /// </summary>
    public static IHostApplicationBuilder AddGameDatabase(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<GameDbContext>(ConnectionName);
        return builder;
    }
}
