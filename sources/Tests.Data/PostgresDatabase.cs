using Data.Database;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Tests.Data;

/// <summary>
/// A fresh PostgreSQL database per test class, migrated with all EF Core migrations and dropped afterwards.
/// Needs the server connection in GAMEHUB_TEST_POSTGRES (e.g. "Host=localhost;Username=postgres;Password=postgres");
/// without it the integration tests are skipped (CI provides a PostgreSQL service).
/// </summary>
public sealed class PostgresDatabase : Xunit.IAsyncLifetime
{
    public const string ServerVariable = "GAMEHUB_TEST_POSTGRES";

    private readonly string? _serverConnection = Environment.GetEnvironmentVariable(ServerVariable);
    private readonly string _databaseName = $"gamehub_test_{Guid.NewGuid():N}";

    public bool IsAvailable => !string.IsNullOrWhiteSpace(_serverConnection);

    public string ConnectionString => new NpgsqlConnectionStringBuilder(_serverConnection) { Database = _databaseName }.ConnectionString;

    public GameDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<GameDbContext>().UseNpgsql(ConnectionString).Options);

    public async ValueTask InitializeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await using (var server = new NpgsqlConnection(_serverConnection))
        {
            await server.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", server);
            await create.ExecuteNonQueryAsync();
        }

        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        NpgsqlConnection.ClearAllPools();
        await using var server = new NpgsqlConnection(_serverConnection);
        await server.OpenAsync();
        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", server);
        await drop.ExecuteNonQueryAsync();
    }
}
