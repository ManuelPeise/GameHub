using Shared.Models.Games;

namespace Tests.Shared;

/// <summary>
/// Keeps Web.App/src/api/generated/contracts.ts in sync with Shared.Models. Locally the test rewrites the file;
/// in CI it fails if the committed file is outdated (GAMEHUB_UPDATE_CONTRACTS=1 forces writing).
/// </summary>
public sealed class TypeScriptContractTests
{
    private static readonly string[] Namespaces = ["Shared.Models.Games", "Shared.Models.Realtime", "Shared.Models.Statistics", "Shared.Models.Administration"];

    [Xunit.Fact]
    public void GeneratedTypeScriptContracts_AreUpToDate()
    {
        var expected = TypeScriptContractGenerator.Generate(typeof(SessionDto).Assembly, Namespaces);
        var path = Path.Combine(FindSourcesDirectory(), "Web.App", "src", "api", "generated", "contracts.ts");

        // Locally the file is simply regenerated (commit it together with the DTO change). In CI (GitHub sets CI=true)
        // it is only compared, so a forgotten regeneration fails the build.
        var isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
        var update = Environment.GetEnvironmentVariable("GAMEHUB_UPDATE_CONTRACTS") == "1" || !isCi;
        if (update || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        var actual = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        Xunit.Assert.True(
            actual == expected,
            $"{path} is outdated. Run the tests locally (they regenerate the file) and commit it.");
    }

    private static string FindSourcesDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GameHubApp.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("sources directory (GameHubApp.slnx) not found.");
    }
}
