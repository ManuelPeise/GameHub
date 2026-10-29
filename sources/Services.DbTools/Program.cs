// Command line tool for database tasks.
//
//   migrate [--seed-development]
//       Applies all pending EF Core migrations (started by Services.AppHost before the API; in Docker as
//       container gamehub-migrate, see US-030). With --seed-development and environment Development it
//       then creates the development accounts (admin@gamehub.local, spieler1/2@gamehub.local) with the
//       password from App:Environment:SeedPassword.
//
//   create-admin <email> <display-name>
//       Creates the first administrator (confirmed e-mail, roles Administrator and Player). The password is
//       taken from the environment variable GAMEHUB_ADMIN_PASSWORD or generated and printed once.
//       An existing account only gets the role.
//
// Anonymizing backups follows with US-031.

using System.Security.Cryptography;
using Data.Database;
using Data.DataAccessor.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shared.Models.Interfaces;
using Shared.Models.Settings;

const string Usage = """
    Usage:
      Services.DbTools migrate [--seed-development]
      Services.DbTools create-admin <email> <display-name>
    """;

if (args.Length == 0)
{
    Console.Error.WriteLine(Usage);
    return 1;
}

// No args here: commands are not configuration values.
var builder = Host.CreateApplicationBuilder();
var appSettings = new AppSettings(builder.Configuration);
builder.Services.AddSingleton<IAppSettings>(appSettings);
builder.AddGameDatabase();
builder.AddUserAccounts(appSettings);

using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var services = scope.ServiceProvider;

switch (args)
{
    case ["migrate"]:
        await MigrateAsync(services);
        return 0;

    case ["migrate", "--seed-development"]:
        await MigrateAsync(services);
        return await SeedDevelopmentAsync(services, appSettings);

    case ["create-admin", var email, var displayName]:
        return await CreateAdminAsync(services, email, displayName);

    default:
        Console.Error.WriteLine(Usage);
        return 1;
}

static async Task MigrateAsync(IServiceProvider services)
{
    var database = services.GetRequiredService<GameDbContext>();
    var pending = (await database.Database.GetPendingMigrationsAsync()).ToList();

    await database.Database.MigrateAsync();

    Console.WriteLine(pending.Count == 0
        ? "Database is up to date."
        : $"Applied {pending.Count} migration(s): {string.Join(", ", pending)}");
}

static async Task<int> SeedDevelopmentAsync(IServiceProvider services, IAppSettings appSettings)
{
    if (appSettings.Environment.Name != EnvironmentSettings.Development)
    {
        Console.Error.WriteLine("Seeding is only allowed in Development.");
        return 1;
    }

    var password = appSettings.Environment.SeedPassword;
    if (string.IsNullOrWhiteSpace(password))
    {
        Console.Error.WriteLine("App:Environment:SeedPassword is not set; development accounts were not created.");
        return 1;
    }

    var seeder = services.GetRequiredService<AccountSeeder>();
    foreach (var (email, displayName, isAdministrator) in AccountSeeder.DevelopmentAccounts)
    {
        var result = await seeder.EnsureAccountAsync(email, displayName, password, isAdministrator);
        if (!result.Succeeded)
        {
            Console.Error.WriteLine($"Seeding {email} failed: {string.Join("; ", result.Errors)}");
            return 1;
        }

        Console.WriteLine(result.Created ? $"Created {email}." : $"{email} exists.");
    }

    return 0;
}

static async Task<int> CreateAdminAsync(IServiceProvider services, string email, string displayName)
{
    var password = Environment.GetEnvironmentVariable("GAMEHUB_ADMIN_PASSWORD");
    var generated = string.IsNullOrWhiteSpace(password);
    if (generated)
    {
        password = GeneratePassword();
    }

    var seeder = services.GetRequiredService<AccountSeeder>();
    var result = await seeder.EnsureAccountAsync(email.Trim(), displayName.Trim(), password!, isAdministrator: true);

    if (!result.Succeeded)
    {
        Console.Error.WriteLine($"Creating the administrator failed: {string.Join("; ", result.Errors)}");
        return 1;
    }

    if (!result.Created)
    {
        Console.WriteLine($"{email} already exists; role Administrator ensured, password unchanged.");
    }
    else if (generated)
    {
        Console.WriteLine($"Administrator {email} created. Password (shown only once): {password}");
    }
    else
    {
        Console.WriteLine($"Administrator {email} created with the password from GAMEHUB_ADMIN_PASSWORD.");
    }

    Console.WriteLine("Two-factor authentication must be set up at the first sign-in to the admin area.");
    return 0;
}

static string GeneratePassword()
{
    // 20 characters with upper case, lower case and digits (no look-alike characters).
    const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    const string lower = "abcdefghijkmnopqrstuvwxyz";
    const string digits = "23456789";
    const string all = upper + lower + digits;

    var characters = new char[20];
    characters[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
    characters[1] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
    characters[2] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
    for (var i = 3; i < characters.Length; i++)
    {
        characters[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
    }

    RandomNumberGenerator.Shuffle(characters.AsSpan());
    return new string(characters);
}
