using System.Text.RegularExpressions;

namespace Tests.Architecture;

/// <summary>
/// Migrations run only before the app starts (AppHost: Services.DbTools; Docker: gamehub-migrate with the
/// migration bundle), never inside the app itself (US-030).
/// </summary>
public sealed partial class MigrationRulesTests
{
    private static readonly string[] ProjectsWithoutMigrations =
        ["Services.Api", "Data.DataAccessor", "Logic.Sessions", "Logic.Users", "Logic.Administration", "Services.ServiceDefaults"];

    [GeneratedRegex(@"\.\s*(Migrate|MigrateAsync|EnsureCreated|EnsureCreatedAsync)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex MigrationCall();

    [Xunit.Fact]
    public void TheApp_NeverAppliesMigrations()
    {
        var offenders = ProjectsWithoutMigrations
            .Select(project => Path.Combine(SourceTree.SourcesDirectory, project))
            .Where(Directory.Exists)
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(file => MigrationCall().IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(SourceTree.SourcesDirectory, file))
            .ToList();

        Xunit.Assert.True(offenders.Count == 0, $"Migrations must not run in the app: {string.Join(", ", offenders)}");
    }

    [Xunit.Fact]
    public void DockerMigration_UsesTheBundle_AndBacksUpLiveFirst()
    {
        var docker = Path.Combine(Directory.GetParent(SourceTree.SourcesDirectory)!.FullName, "docker");
        var script = File.ReadAllText(Path.Combine(docker, "migrate.sh"));

        var backup = script.IndexOf("pg_dump", StringComparison.Ordinal);
        var migrate = script.IndexOf("efbundle", StringComparison.Ordinal);
        Xunit.Assert.True(backup >= 0 && migrate > backup, "migrate.sh must back up (pg_dump) before running the bundle.");
        Xunit.Assert.Contains("PreMigration", script, StringComparison.Ordinal);
        Xunit.Assert.Contains("set -eu", script, StringComparison.Ordinal);

        foreach (var compose in new[] { "compose.testing.yaml", "compose.live.yaml" })
        {
            var text = File.ReadAllText(Path.Combine(docker, compose));
            Xunit.Assert.Contains("condition: service_completed_successfully", text, StringComparison.Ordinal);
        }
    }
}
