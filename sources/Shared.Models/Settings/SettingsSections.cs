namespace Shared.Models.Settings;

/// <summary>Game flow limits (section App:Game).</summary>
public sealed class GameSettings
{
    /// <summary>Time a connected player has for a move in multiplayer before being removed.</summary>
    public TimeSpan TurnTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>Server-side grace period after a lost connection before the player is removed.</summary>
    public TimeSpan ReconnectTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Reconnect attempts of the client within <see cref="ReconnectTimeout"/>.</summary>
    public int ReconnectAttempts { get; set; } = 5;

    /// <summary>Inactivity after which a paused singleplayer session is aborted.</summary>
    public TimeSpan PausedSessionRetention { get; set; } = TimeSpan.FromDays(30);
}

/// <summary>AI behaviour (section App:Ai).</summary>
public sealed class AiSettings
{
    /// <summary>Fixed delay between AI actions (Kniffel); 0 in tests.</summary>
    public TimeSpan ActionDelay { get; set; } = TimeSpan.FromMilliseconds(600);

    /// <summary>Strategy version per game, stored with every AI action.</summary>
    public Dictionary<string, string> StrategyVersions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TicTacToe"] = "1",
        ["Kniffel"] = "1",
    };
}

/// <summary>Outgoing mail via SMTP (section App:Email). Password only from secrets/variables.</summary>
public sealed class EmailSettings
{
    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 25;

    public bool UseTls { get; set; }

    public string SenderAddress { get; set; } = "noreply@gamehub.local";

    public string SenderName { get; set; } = "GameHub";

    public string? UserName { get; set; }

    public string? Password { get; set; }
}

/// <summary>Database backups (section App:Backup).</summary>
public sealed class BackupSettings
{
    /// <summary>Time of day (server local time) for the automatic daily backup.</summary>
    public TimeOnly DailyAt { get; set; } = new(3, 0);

    public string Directory { get; set; } = "/backups";

    /// <summary>Second system the backups are copied to; empty = no copy.</summary>
    public string? OffsiteTarget { get; set; }

    public int KeepDaily { get; set; } = 7;

    public int KeepWeekly { get; set; } = 4;

    public int KeepMonthly { get; set; } = 12;
}

/// <summary>Application log in the database (section App:Logging).</summary>
public sealed class LoggingSettings
{
    /// <summary>Minimum level written to the database: Trace, Debug, Information, Warning, Error or Critical.</summary>
    public string DatabaseMinimumLevel { get; set; } = "Warning";

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(90);
}

/// <summary>Security limits (section App:Security).</summary>
public sealed class SecuritySettings
{
    public bool RequireAdminTwoFactor { get; set; } = true;

    /// <summary>Registrations per client IP and hour.</summary>
    public int RegistrationRateLimit { get; set; } = 5;

    /// <summary>Login attempts per client IP and minute.</summary>
    public int LoginRateLimit { get; set; } = 10;

    /// <summary>Failed password attempts after which an account is locked temporarily.</summary>
    public int LockoutMaxFailedAttempts { get; set; } = 5;

    /// <summary>Duration of the temporary lock after too many failed attempts.</summary>
    public TimeSpan LockoutDuration { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Validity of links in e-mails (confirm address, reset password).</summary>
    public TimeSpan AccountTokenLifetime { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>Environment information (section App:Environment).</summary>
public sealed class EnvironmentSettings
{
    public const string Development = "Development";
    public const string Testing = "Testing";
    public const string Live = "Live";

    /// <summary>Development, Testing or Live.</summary>
    public string Name { get; set; } = Development;

    /// <summary>
    /// Public address of Web.App, used for links in e-mails (e.g. https://gamehub.example.org).
    /// Set by Aspire in development; required in Testing and Live.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    /// <summary>
    /// Password of the seeded development accounts (Development only, set by Aspire from the user
    /// secrets of Services.AppHost). Never set in Testing or Live.
    /// </summary>
    public string? SeedPassword { get; set; }

    /// <summary>Shows the environment banner in the UI (always off in Live).</summary>
    public bool ShowBanner { get; set; } = true;

    public bool IsLive => string.Equals(Name, Live, StringComparison.Ordinal);
}
