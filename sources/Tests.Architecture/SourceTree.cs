using System.Xml.Linq;

namespace Tests.Architecture;

/// <summary>A project below sources/ with its direct references, read from the project file.</summary>
internal sealed record ProjectInfo(
    string Name,
    string FolderName,
    string FilePath,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences);

/// <summary>Reads the solution and all project files below sources/.</summary>
internal static class SourceTree
{
    public const string SolutionFileName = "GameHubApp.slnx";

    private static readonly Lazy<string> SourcesDirectoryLazy = new(FindSourcesDirectory);
    private static readonly Lazy<IReadOnlyList<ProjectInfo>> ProjectsLazy = new(LoadProjects);
    private static readonly Lazy<IReadOnlyDictionary<string, string>> SolutionFoldersLazy = new(LoadSolutionFolders);

    public static string SourcesDirectory => SourcesDirectoryLazy.Value;

    public static IReadOnlyList<ProjectInfo> Projects => ProjectsLazy.Value;

    /// <summary>Project name to solution folder (e.g. "/03 Logic/"), as listed in the solution file.</summary>
    public static IReadOnlyDictionary<string, string> SolutionFolders => SolutionFoldersLazy.Value;

    private static string FindSourcesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"{SolutionFileName} not found above {AppContext.BaseDirectory}.");
    }

    private static IReadOnlyList<ProjectInfo> LoadProjects()
    {
        var projects = new List<ProjectInfo>();

        // Projects live directly below sources/, one folder per project.
        foreach (var directory in Directory.EnumerateDirectories(SourcesDirectory))
        {
            var folderName = Path.GetFileName(directory);
            if (folderName.StartsWith('.'))
            {
                continue;
            }

            var projectFiles = Directory.EnumerateFiles(directory, "*.csproj")
                .Concat(Directory.EnumerateFiles(directory, "*.esproj"));

            foreach (var projectFile in projectFiles)
            {
                projects.Add(ReadProject(projectFile, folderName));
            }
        }

        return projects;
    }

    private static ProjectInfo ReadProject(string projectFile, string folderName)
    {
        var document = XDocument.Load(projectFile);

        var projectReferences = document.Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .ToList();

        var packageReferences = document.Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .OfType<string>()
            .ToList();

        return new ProjectInfo(
            Path.GetFileNameWithoutExtension(projectFile),
            folderName,
            projectFile,
            projectReferences,
            packageReferences);
    }

    private static IReadOnlyDictionary<string, string> LoadSolutionFolders()
    {
        var document = XDocument.Load(Path.Combine(SourcesDirectory, SolutionFileName));
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var project in document.Descendants("Project"))
        {
            var path = (string?)project.Attribute("Path");
            if (path is null)
            {
                continue;
            }

            var folder = (string?)project.Parent?.Attribute("Name") ?? "/";
            result[Path.GetFileNameWithoutExtension(path.Replace('\\', '/'))] = folder;
        }

        return result;
    }
}
