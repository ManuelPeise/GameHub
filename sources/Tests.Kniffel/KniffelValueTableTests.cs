using Logic.Kniffel.Ai;

namespace Tests.Kniffel;

public sealed class KniffelValueTableTests
{
    [Xunit.Fact]
    public void StartValue_IsTheKnownOptimum()
    {
        // Optimal solitaire play (upper bonus 35, 50 per further Kniffel, joker rule) averages 250.0 points.
        var start = KniffelValueTable.Value(0, 0, kniffelBonusEligible: false);

        Xunit.Assert.InRange(start, 249.5, 250.5);
    }

    [Xunit.Fact]
    public void Values_GrowWithBetterSituations()
    {
        // Only "Chance" open: about the expected sum of five dice with up to three rolls.
        var onlyChanceOpen = KniffelValueTable.Value(0b0_1111_1111_1111, 0, false);
        Xunit.Assert.InRange(onlyChanceOpen, 20.0, 26.0);

        // With the upper bonus already secured, the rest is worth less than with an empty sheet,
        // but a Kniffel bonus chance (eligible) adds value.
        Xunit.Assert.True(KniffelValueTable.Value(1 << 11, 0, true) > KniffelValueTable.Value(1 << 11, 0, false));
    }

    /// <summary>
    /// Regenerates the embedded table (takes several minutes). Run explicitly after changing the rules:
    /// dotnet test Tests.Kniffel --filter "FullyQualifiedName~RegenerateTable" -- xUnit.Explicit=only
    /// </summary>
    [Xunit.Fact(Explicit = true)]
    public void RegenerateTable()
    {
        var values = KniffelValueTableGenerator.Generate(Xunit.TestContext.Current.CancellationToken);
        var path = Path.Combine(FindSources(), "Logic.Kniffel", "Ai", "kniffel-values.bin.gz");
        using (var file = File.Create(path))
        {
            KniffelValueTable.Write(values, file);
        }

        Xunit.Assert.InRange(values[KniffelValueTable.Index(0, 0, false)], 249.5, 250.5);
    }

    private static string FindSources()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "GameHubApp.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("sources directory not found.");
    }
}
