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

    private static readonly string[] EnvironmentNames =
    [
        EnvironmentSettings.Development, EnvironmentSettings.Testing, EnvironmentSettings.Live,
    ];

    private static readonly string[] LogLevels = ["Trace", "Debug", "Information", "Warning", "Error", "Critical"];

    public AppSettings(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        configuration.GetSection(SectionName).Bind(this);

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

        Require(!string.IsNullOrWhiteSpace(Backup.Directory), "Backup:Directory", "is required");
        Require(Backup.KeepDaily >= 0, "Backup:KeepDaily", "must not be negative");
        Require(Backup.KeepWeekly >= 0, "Backup:KeepWeekly", "must not be negative");
        Require(Backup.KeepMonthly >= 0, "Backup:KeepMonthly", "must not be negative");

        Require(LogLevels.Contains(Logging.DatabaseMinimumLevel), "Logging:DatabaseMinimumLevel",
            $"must be one of {string.Join(", ", LogLevels)}");
        Require(Logging.Retention > TimeSpan.Zero, "Logging:Retention", "must be greater than 0");

        Require(Security.RegistrationRateLimit > 0, "Security:RegistrationRateLimit", "must be greater than 0");
        Require(Security.LoginRateLimit > 0, "Security:LoginRateLimit", "must be greater than 0");
        Require(!Environment.IsLive || Security.RequireAdminTwoFactor, "Security:RequireAdminTwoFactor",
            "must be true in Live");

        return errors;
    }
}
