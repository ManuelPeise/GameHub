using System.Text.RegularExpressions;

namespace Tests.Architecture;

/// <summary>
/// Enforces the project structure and dependency rules from the concept (section "Projektstruktur")
/// and from CLAUDE.md. Rules are checked on the project files, so they also catch references that
/// are not (yet) used in code.
/// </summary>
public sealed partial class ProjectStructureTests
{
    private const string TestPrefix = "Tests.";

    private static readonly string[] GameProjects = ["Logic.TicTacToe", "Logic.Kniffel", "Logic.Muehle"];

    private static readonly string[] AllLogicProjects =
    [
        "Logic.Core", .. GameProjects, "Logic.Shared", "Logic.Sessions", "Logic.Users", "Logic.Administration",
    ];

    private static readonly string[] ApplicationLogicReferences =
    [
        "Logic.Core", "Logic.Shared", .. GameProjects, "Shared.Models",
    ];

    /// <summary>Allowed direct project references per project (test projects are handled separately).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> AllowedReferences = new Dictionary<string, string[]>
    {
        ["Web.App"] = [],
        ["Services.Api"] = [.. AllLogicProjects, "Data.DataAccessor", "Shared.Models", "Services.ServiceDefaults"],
        ["Services.AppHost"] = ["Services.Api", "Services.DbTools"],
        ["Services.ServiceDefaults"] = [],
        ["Services.DbTools"] = ["Logic.Users", "Data.DataAccessor", "Shared.Models"],
        ["Logic.Core"] = [],
        ["Logic.TicTacToe"] = ["Logic.Core"],
        ["Logic.Kniffel"] = ["Logic.Core"],
        ["Logic.Muehle"] = ["Logic.Core"],
        ["Logic.Shared"] = ["Shared.Models"],
        ["Logic.Sessions"] = ApplicationLogicReferences,
        ["Logic.Users"] = ApplicationLogicReferences,
        ["Logic.Administration"] = ApplicationLogicReferences,
        ["Data.Database"] = ["Shared.Models"],
        ["Data.DataAccessor"] = ["Data.Database", "Logic.Shared", "Shared.Models"],
        ["Shared.Models"] = [],
    };

    /// <summary>Solution folder per name prefix (area).</summary>
    private static readonly IReadOnlyDictionary<string, string> SolutionFolderByArea = new Dictionary<string, string>
    {
        ["Web"] = "/01 Web/",
        ["Services"] = "/02 Services/",
        ["Logic"] = "/03 Logic/",
        ["Data"] = "/04 Data/",
        ["Shared"] = "/05 Shared/",
        ["Tests"] = "/06 Tests/",
    };

    /// <summary>Projects that hold game rules and the game contracts: pure code, no packages.</summary>
    private static readonly string[] PureProjects = ["Logic.Core", .. GameProjects];

    [GeneratedRegex(@"^(Web|Services|Logic|Data|Shared|Tests)\.[A-Z][A-Za-z0-9]*$")]
    private static partial Regex ProjectNamePattern();

    [Xunit.Fact]
    public void Projects_AreFound()
    {
        Xunit.Assert.NotEmpty(SourceTree.Projects);
    }

    [Xunit.Fact]
    public void ProjectNames_FollowAreaNameSchemeAndMatchTheirFolder()
    {
        var violations = SourceTree.Projects
            .Where(project => !ProjectNamePattern().IsMatch(project.Name) || project.Name != project.FolderName)
            .Select(project => $"{project.FolderName}/{Path.GetFileName(project.FilePath)}: expected <Area>.<Name> in a folder of the same name")
            .ToList();

        Xunit.Assert.Empty(violations);
    }

    [Xunit.Fact]
    public void EveryProject_HasDependencyRule()
    {
        var missing = SourceTree.Projects
            .Where(project => !IsTestProject(project) && !AllowedReferences.ContainsKey(project.Name))
            .Select(project => $"{project.Name}: add a rule to AllowedReferences (and to the concept / CLAUDE.md)")
            .ToList();

        Xunit.Assert.Empty(missing);
    }

    [Xunit.Fact]
    public void ProjectReferences_FollowDependencyRules()
    {
        var violations = new List<string>();

        foreach (var project in SourceTree.Projects)
        {
            foreach (var reference in project.ProjectReferences)
            {
                if (!IsAllowed(project, reference))
                {
                    violations.Add($"{project.Name} must not reference {reference}");
                }
            }
        }

        Xunit.Assert.Empty(violations);
    }

    [Xunit.Fact]
    public void GameRulesAndContracts_HaveNoPackageReferences()
    {
        var violations = SourceTree.Projects
            .Where(project => PureProjects.Contains(project.Name) && project.PackageReferences.Count > 0)
            .Select(project => $"{project.Name} references packages: {string.Join(", ", project.PackageReferences)}")
            .ToList();

        Xunit.Assert.Empty(violations);
    }

    [Xunit.Fact]
    public void EveryProject_IsInSolutionFolderOfItsArea()
    {
        var violations = new List<string>();

        foreach (var project in SourceTree.Projects)
        {
            var area = project.Name.Split('.')[0];
            if (!SolutionFolderByArea.TryGetValue(area, out var expectedFolder))
            {
                continue; // reported by the naming test
            }

            if (!SourceTree.SolutionFolders.TryGetValue(project.Name, out var actualFolder))
            {
                violations.Add($"{project.Name} is missing in {SourceTree.SolutionFileName}");
            }
            else if (actualFolder != expectedFolder)
            {
                violations.Add($"{project.Name} is in {actualFolder}, expected {expectedFolder}");
            }
        }

        Xunit.Assert.Empty(violations);
    }

    private static bool IsTestProject(ProjectInfo project) => project.Name.StartsWith(TestPrefix, StringComparison.Ordinal);

    private static bool IsAllowed(ProjectInfo project, string reference)
    {
        if (IsTestProject(project))
        {
            // Tests may reference any production project, but not other test projects.
            return !reference.StartsWith(TestPrefix, StringComparison.Ordinal);
        }

        return AllowedReferences.TryGetValue(project.Name, out var allowed) && allowed.Contains(reference);
    }
}
