using System.Runtime.CompilerServices;

namespace FinanceTracker.Tests.Architecture.Helpers;

/// <summary>
/// Reads project sources from disk for the architecture rules that reflection cannot express.
/// </summary>
internal static class SourceScan
{
	private const string SolutionFilePattern = "*.slnx";

	private static string? _cachedRoot;

	internal static string RepositoryRoot([CallerFilePath] string callerFilePath = "")
	{
		if (_cachedRoot is not null)
			return _cachedRoot;

		DirectoryInfo? current = Directory.GetParent(path: callerFilePath);

		while (current is not null)
		{
			if (current.EnumerateFiles(searchPattern: SolutionFilePattern).Any())
				return _cachedRoot = current.FullName;

			current = current.Parent;
		}

		throw new InvalidOperationException(
			message: $"No {SolutionFilePattern} was found in any directory above '{callerFilePath}', so the repository root cannot be resolved."
		);
	}

	internal static string ProjectRoot(string projectName)
		=> Path.Combine(path1: RepositoryRoot(), path2: projectName);

	internal static IEnumerable<string> FilesIn(string projectName)
	{
		string projectRoot = ProjectRoot(projectName: projectName);

		if (!Directory.Exists(path: projectRoot))
			throw new DirectoryNotFoundException(message: $"Project '{projectName}' was not found at {projectRoot}. A rule scanning it would silently find nothing.");

		return Directory.EnumerateFiles(path: projectRoot, searchPattern: "*.cs", searchOption: SearchOption.AllDirectories).Where(predicate: path =>
			!path.Contains(value: $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", comparisonType: StringComparison.Ordinal) &&
			!path.Contains(value: $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", comparisonType: StringComparison.Ordinal)
		);
	}

	internal static string ReadFile(string projectName, params string[] pathSegments)
	{
		string path = Path.Combine(paths: [ProjectRoot(projectName: projectName), .. pathSegments]);

		if (File.Exists(path: path))
			return File.ReadAllText(path: path);

		throw new FileNotFoundException(
			message: $"{Path.GetFileName(path: path)} was not found at {path}. Without it the rule cannot tell a violation from a clean file.",
			fileName: path
		);
	}

	internal static string StripComments(string source)
		=> String.Join(separator: "\n", values: source.Split(separator: '\n').Select(selector: StripComment));

	internal static string StripComment(string line)
	{
		int index = line.IndexOf(value: "//", comparisonType: StringComparison.Ordinal);
		return index < 0 ? line : line[..index];
	}
}
