using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace FinanceTracker.Tests.Architecture;

public sealed class SaveChangesIsolationArchitectureTests
{
	private const string InfrastructureProject = "FinanceTracker.Infrastructure";
	private static readonly string[] AllowedFiles = ["EFUnitOfWork.cs"];

	private static readonly Regex SaveChangesCall = new Regex(
		pattern: @"\bSaveChanges(Async)?\s*\(",
		options: RegexOptions.Compiled
	);

	internal static IEnumerable<string> SourceFiles(string projectRoot)
	{
		return Directory.EnumerateFiles(path: projectRoot, searchPattern: "*.cs", searchOption: SearchOption.AllDirectories).Where(predicate: path =>
			!path.Contains(value: $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", comparisonType: StringComparison.Ordinal) &&
			!path.Contains(value: $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", comparisonType: StringComparison.Ordinal)
		);
	}

	internal static string StripComment(string line)
	{
		int index = line.IndexOf(value: "//", comparisonType: StringComparison.Ordinal);
		return index < 0 ? line : line[..index];
	}

	internal static string RepositoryRoot([CallerFilePath] string callerFilePath = "")
	{
		// <root>/FinanceTracker.Tests/Architecture/<this file>
		DirectoryInfo? root = Directory.GetParent(path: callerFilePath)?.Parent?.Parent;

		if (root is null)
			throw new InvalidOperationException(message: $"Could not resolve the repository root from '{callerFilePath}'.");

		return root.FullName;
	}

	[Test]
	public async Task Infrastructure_ShouldNotCommitOutsideTheUnitOfWork()
	{
		string projectRoot = Path.Combine(path1: RepositoryRoot(), path2: InfrastructureProject);

		List<string> violations = [];

		foreach (string file in SourceFiles(projectRoot: projectRoot))
		{
			if (AllowedFiles.Contains(value: Path.GetFileName(path: file)))
				continue;

			string[] lines = await File.ReadAllLinesAsync(path: file);

			for (int i = 0; i < lines.Length; i++)
			{
				string code = StripComment(line: lines[i]);

				if (!SaveChangesCall.IsMatch(input: code))
					continue;

				violations.Add(item: $"{Path.GetRelativePath(relativeTo: projectRoot, path: file)}:{i + 1} calls SaveChanges — " +
					"only EFUnitOfWork may commit. Stage the work and let the caller wrap it in " +
					"unitOfWork.ExecuteInTransactionAsync, otherwise a multi-statement write is applied piecemeal."
				);
			}
		}

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: violations));
	}

	[Test]
	public async Task TheUnitOfWorkItself_ShouldStillCommit()
	{
		string unitOfWork = Path.Combine(RepositoryRoot(), InfrastructureProject, "Database", "UnitOfWork", "EFUnitOfWork.cs");

		await Assert.That(value: File.Exists(path: unitOfWork)).IsTrue()
			.Because(message: $"EFUnitOfWork.cs was not found at {unitOfWork} — did it move? The guard above would silently pass against an empty allowlist.");

		string source = await File.ReadAllTextAsync(path: unitOfWork);

		await Assert.That(value: SaveChangesCall.IsMatch(input: source)).IsTrue()
			.Because(message: "EFUnitOfWork no longer calls SaveChanges — either persistence moved elsewhere, or this guard is now testing nothing.");
	}
}
