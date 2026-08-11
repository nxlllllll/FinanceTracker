using System.Text.RegularExpressions;
using FinanceTracker.Tests.Architecture.Helpers;

namespace FinanceTracker.Tests.Architecture;

public sealed class SaveChangesIsolationArchitectureTests
{
	private const string InfrastructureProject = "FinanceTracker.Infrastructure";
	private static readonly string[] AllowedFiles = ["EFUnitOfWork.cs"];

	private static readonly Regex SaveChangesCall = new Regex(
		pattern: @"\bSaveChanges(Async)?\s*\(",
		options: RegexOptions.Compiled
	);

	[Test]
	public async Task Infrastructure_ShouldNotCommitOutsideTheUnitOfWork()
	{
		string projectRoot = SourceScan.ProjectRoot(projectName: InfrastructureProject);

		List<string> violations = [];

		foreach (string file in SourceScan.FilesIn(projectName: InfrastructureProject))
		{
			if (AllowedFiles.Contains(value: Path.GetFileName(path: file)))
				continue;

			string[] lines = await File.ReadAllLinesAsync(path: file);

			for (int i = 0; i < lines.Length; i++)
			{
				string code = SourceScan.StripComment(line: lines[i]);

				if (!SaveChangesCall.IsMatch(input: code))
					continue;

				violations.Add(item: $"{Path.GetRelativePath(relativeTo: projectRoot, path: file)}:{i + 1} calls SaveChanges — " +
					"only EFUnitOfWork may commit. Stage the work and let the caller wrap it in " +
					"unitOfWork.ExecuteInTransactionAsync, otherwise a multi-statement write is applied piecemeal.");
			}
		}

		await Assert.That(value: violations).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: violations));
	}

	[Test]
	public async Task TheUnitOfWorkItself_ShouldStillCommit()
	{
		string source = SourceScan.ReadFile(
			projectName: InfrastructureProject,
			"Database", "UnitOfWork", "EFUnitOfWork.cs"
		);

		await Assert.That(value: SaveChangesCall.IsMatch(input: source)).IsTrue()
			.Because(message: "EFUnitOfWork no longer calls SaveChanges — either persistence moved elsewhere, or this guard is now testing nothing.");
	}
}
