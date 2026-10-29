// Command line tool for database tasks.
//   migrate   applies all pending EF Core migrations (started by Services.AppHost before the API,
//             in Docker as container gamehub-migrate, see US-030)
// Creating the first administrator and anonymizing backups follow with US-007 and US-031.

using Data.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args is not ["migrate"])
{
    Console.Error.WriteLine("Usage: Services.DbTools migrate");
    return 1;
}

// No args here: the command is not a configuration value.
var builder = Host.CreateApplicationBuilder();
builder.AddGameDatabase();

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var database = scope.ServiceProvider.GetRequiredService<GameDbContext>();

var pending = (await database.Database.GetPendingMigrationsAsync()).ToList();
await database.Database.MigrateAsync();

Console.WriteLine(pending.Count == 0
    ? "Database is up to date."
    : $"Applied {pending.Count} migration(s): {string.Join(", ", pending)}");

return 0;
