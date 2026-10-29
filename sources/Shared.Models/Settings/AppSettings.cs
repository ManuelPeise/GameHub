using Microsoft.Extensions.Configuration;
using Shared.Models.Interfaces;

namespace Shared.Models.Settings;

/// <summary>
/// Reads the configuration section "App" into typed sections and validates it.
/// Create it once at startup and register it as <see cref="IAppSettings"/>: invalid settings
/// throw <see cref="AppSettingsValidationException"/> and stop the application from starting.
/// This is the only class that reads <see cref="IConfiguration"/>.
/// </summary>
public sealed class AppSettings : IAppSettings
{
    public const string SectionName = "App";

    /// <summary>Connection string that Aspire injects for the Mailpit resource ("Endpoint=smtp://host:port").</summary>
    public const string MailpitConnectionName = "mailpit";

    private static readonly string[] EnvironmentNames =
    [
        EnvironmentSettings.Development, EnvironmentSettings.Testing, EnvironmentSettings.Live,
    ];

    private static readonly string[] LogLevels = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    public AppSettings(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.GetSection(SectionName).Bind(this);
        ApplyMailpitConnection(configuration.GetConnectionString(MailpitConnectionName));

        var errors = Validate();
        if (errors.Count > 0)
        {
            throw new AppSettingsValidationException(errors);
        }
    }

    public GameSettings Game { get; } = new();

    public AiSettings Ai { get; } = new();

    public EmailSettings Email { get; } = new();

    public BackupSettings Backup { get; } = new();

    public LoggingSettings Logging { get; } = new();

    public SecuritySettings Security { get; } = new();

    public EnvironmentSettings Environment { get; } = new();

    /// <summary>
    /// In development Aspire provides Mailpit only as a connection string. It is used when no SMTP host
    /// is configured explicitly.
    /// </summary>
    private void ApplyMailpitConnection(string? connectionString)
    {
        if (!string.IsNullOrWhiteSpace(Email.Host) || string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0
                || !part[..separator].Equals("Endpoint", StringComparison.OrdinalIgnoreCase)
                || !Uri.TryCreate(part[(separator + 1)..], UriKind.Absolute, out var endpoint))
            {
                continue;
            }

            Email.Host = endpoint.Host;
            if (endpoint.Port > 0)
            {
                Email.Port = endpoint.Port;
            }
        }
    }

    private List<string> Validate()
    {
        var errors = new List<string>();

        void Require(bool condition, string key, string message)
        {
            if (!condition)
            {
                errors.Add($"{SectionName}:{key}: {message}");
            }
        }

        Require(EnvironmentNames.Contains(Environment.Name), "Environment:Name",
            $"must be one of {string.Join(", ", EnvironmentNames)} (was '{Environment.Name}')");

        Require(Game.TurnTimeout > TimeSpan.Zero, "Game:TurnTimeout", "must be greater than 0");
        Require(Game.ReconnectTimeout > TimeSpan.Zero, "Game:ReconnectTimeout", "must be greater than 0");
        Require(Game.ReconnectAttempts > 0, "Game:ReconnectAttempts", "must be greater than 0");
        Require(Game.PausedSessionRetention > TimeSpan.Zero, "Game:PausedSessionRetention", "must be greater than 0");

        Require(Ai.ActionDelay >= TimeSpan.Zero, "Ai:ActionDelay", "must not be negative");

        Require(!Environment.IsLive || !string.IsNullOrWhiteSpace(Email.Host), "Email:Host", "is required in Live");
        Require(Email.Port is > 0 and <= 65535, "Email:Port", "must be between 1 and 65535");
        Require(!string.IsNullOrWhiteSpace(Email.SenderAddress), "Email:SenderAddress", "is required");

        Require(Environment.Name == EnvironmentSettings.Development || IsAbsoluteHttpUrl(Environment.PublicBaseUrl),
            "Environment:PublicBaseUrl", "is required as absolute http(s) URL outside Development");
        Require(string.IsNullOrEmpty(Environment.PublicBaseUrl) || IsAbsoluteHttpUrl(Environment.PublicBaseUrl),
            "Environment:PublicBaseUrl", "must be an absolute http(s) URL");

        Require(Environment.Name == EnvironmentSettings.Development || string.IsNullOrEmpty(Environment.SeedPassword),
            "Environment:SeedPassword", "is only allowed in Development");

        Require(!string.IsNullOrWhiteSpace(Backup.Directory), "Backup:Directory", "is required");
        Require(Backup.KeepDaily >= 0, "Backup:KeepDaily", "must not be negative");
        Require(Backup.KeepWeekly >= 0, "Backup:KeepWeekly", "must not be negative");
        Require(Backup.KeepMonthly >= 0, "Backup:KeepMonthly", "must not be negative");

        Require(LogLevels.Contains(Logging.DatabaseMinimumLevel), "Logging:DatabaseMinimumLevel",
            $"must be one of {string.Join(", ", LogLevels)}");
        Require(Logging.Retention > TimeSpan.Zero, "Logging:Retention", "must be greater than 0");

        Require(Security.RegistrationRateLimit > 0, "Security:RegistrationRateLimit", "must be greater than 0");
        Require(Security.LoginRateLimit > 0, "Security:LoginRateLimit", "must be greater than 0");
        Require(Security.LockoutMaxFailedAttempts > 0, "Security:LockoutMaxFailedAttempts", "must be greater than 0");
        Require(Security.LockoutDuration > TimeSpan.Zero, "Security:LockoutDuration", "must be greater than 0");
        Require(Security.AccountTokenLifetime > TimeSpan.Zero, "Security:AccountTokenLifetime", "must be greater than 0");
        Require(!Environment.IsLive || Security.RequireAdminTwoFactor, "Security:RequireAdminTwoFactor",
            "must be true in Live");

        return errors;
    }

    private static bool IsAbsoluteHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
