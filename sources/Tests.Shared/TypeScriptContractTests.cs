using Shared.Models.Games;

namespace Tests.Shared;

/// <summary>
/// Keeps Web.App/src/api/generated/contracts.ts in sync with Shared.Models. Fails if the file is outdated;
/// with GAMEHUB_UPDATE_CONTRACTS=1 (or if the file does not exist yet) it writes the file instead.
/// </summary>
public sealed class TypeScriptContractTests
{
    private static readonly string[] Namespaces = ["Shared.Models.Games", "Shared.Models.Realtime"];

    [Xunit.Fact]
    public void GeneratedTypeScriptContracts_AreUpToDate()
    {
        var expected = TypeScriptContractGenerator.Generate(typeof(SessionDto).Assembly, Namespaces);
        var path = Path.Combine(FindSourcesDirectory(), "Web.App", "src", "api", "generated", "contracts.ts");

        var update = Environment.GetEnvironmentVariable("GAMEHUB_UPDATE_CONTRACTS") == "1";
        if (update || !File.Exists(path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, expected, new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        var actual = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        Xunit.Assert.True(
            actual == expected,
            $"{path} is outdated. Run the tests once with GAMEHUB_UPDATE_CONTRACTS=1 and commit the file.");
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
