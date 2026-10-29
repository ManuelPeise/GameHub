using Shared.Models.Settings;

namespace Shared.Models.Interfaces;

/// <summary>
/// Typed, validated application settings (configuration section "App").
/// The only way for services and logic classes to read configuration; see CLAUDE.md rule 7.
/// </summary>
public interface IAppSettings
{
    GameSettings Game { get; }

    AiSettings Ai { get; }

    EmailSettings Email { get; }

    BackupSettings Backup { get; }

    LoggingSettings Logging { get; }

    SecuritySettings Security { get; }

    EnvironmentSettings Environment { get; }
}
