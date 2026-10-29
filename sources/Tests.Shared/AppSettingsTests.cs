using Microsoft.Extensions.Configuration;
using Shared.Models.Settings;

namespace Tests.Shared;

public sealed class AppSettingsTests
{
    private static AppSettings Create(params (string Key, string? Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

        return new AppSettings(configuration);
    }

    private static AppSettingsValidationException CreateInvalid(params (string Key, string? Value)[] values) =>
        Xunit.Assert.Throws<AppSettingsValidationException>(() => Create(values));

    [Xunit.Fact]
    public void WithoutAppSection_UsesDefaultsFromConcept()
    {
        var settings = Create();

        Xunit.Assert.Equal(TimeSpan.FromMinutes(2), settings.Game.TurnTimeout);
        Xunit.Assert.Equal(TimeSpan.FromSeconds(60), settings.Game.ReconnectTimeout);
        Xunit.Assert.Equal(5, settings.Game.ReconnectAttempts);
        Xunit.Assert.Equal(TimeSpan.FromDays(30), settings.Game.PausedSessionRetention);
        Xunit.Assert.Equal(TimeSpan.FromMilliseconds(600), settings.Ai.ActionDelay);
        Xunit.Assert.Equal(new TimeOnly(3, 0), settings.Backup.DailyAt);
        Xunit.Assert.Equal((7, 4, 12), (settings.Backup.KeepDaily, settings.Backup.KeepWeekly, settings.Backup.KeepMonthly));
        Xunit.Assert.Equal("Warning", settings.Logging.DatabaseMinimumLevel);
        Xunit.Assert.Equal(TimeSpan.FromDays(90), settings.Logging.Retention);
        Xunit.Assert.True(settings.Security.RequireAdminTwoFactor);
        Xunit.Assert.Equal(EnvironmentSettings.Development, settings.Environment.Name);
    }

    [Xunit.Fact]
    public void BindsValuesFromAppSection()
    {
        var settings = Create(
            ("App:Game:TurnTimeout", "00:01:30"),
            ("App:Ai:ActionDelay", "00:00:00"),
            ("App:Ai:StrategyVersions:Kniffel", "2"),
            ("App:Email:Host", "smtp.example.org"),
            ("App:Email:Port", "587"),
            ("App:Environment:Name", "Testing"));

        Xunit.Assert.Equal(TimeSpan.FromSeconds(90), settings.Game.TurnTimeout);
        Xunit.Assert.Equal(TimeSpan.Zero, settings.Ai.ActionDelay);
        Xunit.Assert.Equal("2", settings.Ai.StrategyVersions["Kniffel"]);
        Xunit.Assert.Equal("1", settings.Ai.StrategyVersions["TicTacToe"]);
        Xunit.Assert.Equal("smtp.example.org", settings.Email.Host);
        Xunit.Assert.Equal(587, settings.Email.Port);
        Xunit.Assert.Equal(EnvironmentSettings.Testing, settings.Environment.Name);
    }

    [Xunit.Fact]
    public void Live_WithoutSmtpHost_FailsAndNamesTheKey()
    {
        var exception = CreateInvalid(("App:Environment:Name", "Live"));

        Xunit.Assert.Contains(exception.Errors, error => error.StartsWith("App:Email:Host:", StringComparison.Ordinal));
    }

    [Xunit.Fact]
    public void Live_WithSmtpHost_IsValid()
    {
        var settings = Create(("App:Environment:Name", "Live"), ("App:Email:Host", "smtp.example.org"));

        Xunit.Assert.True(settings.Environment.IsLive);
    }

    [Xunit.Fact]
    public void Live_WithoutAdminTwoFactor_Fails()
    {
        var exception = CreateInvalid(
            ("App:Environment:Name", "Live"),
            ("App:Email:Host", "smtp.example.org"),
            ("App:Security:RequireAdminTwoFactor", "false"));

        Xunit.Assert.Contains(exception.Errors, error => error.StartsWith("App:Security:RequireAdminTwoFactor:", StringComparison.Ordinal));
    }

    [Xunit.Theory]
    [Xunit.InlineData("App:Environment:Name", "Production", "App:Environment:Name:")]
    [Xunit.InlineData("App:Game:TurnTimeout", "00:00:00", "App:Game:TurnTimeout:")]
    [Xunit.InlineData("App:Game:ReconnectAttempts", "0", "App:Game:ReconnectAttempts:")]
    [Xunit.InlineData("App:Ai:ActionDelay", "-00:00:01", "App:Ai:ActionDelay:")]
    [Xunit.InlineData("App:Email:Port", "70000", "App:Email:Port:")]
    [Xunit.InlineData("App:Logging:DatabaseMinimumLevel", "Verbose", "App:Logging:DatabaseMinimumLevel:")]
    [Xunit.InlineData("App:Backup:KeepDaily", "-1", "App:Backup:KeepDaily:")]
    [Xunit.InlineData("App:Security:LoginRateLimit", "0", "App:Security:LoginRateLimit:")]
    public void InvalidValue_FailsAndNamesTheKey(string key, string value, string expectedPrefix)
    {
        var exception = CreateInvalid((key, value));

        Xunit.Assert.Contains(exception.Errors, error => error.StartsWith(expectedPrefix, StringComparison.Ordinal));
        Xunit.Assert.Contains(expectedPrefix, exception.Message, StringComparison.Ordinal);
    }

    [Xunit.Fact]
    public void MultipleProblems_AreAllReported()
    {
        var exception = CreateInvalid(("App:Game:ReconnectAttempts", "0"), ("App:Security:LoginRateLimit", "0"));

        Xunit.Assert.Equal(2, exception.Errors.Count);
    }
}
