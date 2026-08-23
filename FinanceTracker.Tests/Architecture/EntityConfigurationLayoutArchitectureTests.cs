using System.Text.RegularExpressions;
using FinanceTracker.Tests.Architecture.Helpers;

namespace FinanceTracker.Tests.Architecture;

public sealed class EntityConfigurationLayoutArchitectureTests
{
	private const string InfrastructureProject = "FinanceTracker.Infrastructure";

	private static readonly Regex ConfigurationDeclaration = new Regex(
		pattern: @"class\s+(\w+)\s*:\s*IEntityTypeConfiguration<(\w+)>",
		options: RegexOptions.Compiled
	);

	private sealed record Declaration(string FilePath, string FileName, string ClassName, string EntityName);

	private static List<Declaration> FindDeclarations()
	{
		List<Declaration> declarations = [];

		foreach (string path in SourceScan.FilesIn(projectName: InfrastructureProject))
		{
			string source = SourceScan.StripComments(source: File.ReadAllText(path: path));

			foreach (Match match in ConfigurationDeclaration.Matches(input: source))
			{
				declarations.Add(item: new Declaration(
					FilePath: path,
					FileName: Path.GetFileNameWithoutExtension(path: path),
					ClassName: match.Groups[1].Value,
					EntityName: match.Groups[2].Value
				));
			}
		}

		return declarations;
	}

	[Test]
	public async Task EveryConfiguration_ShouldLiveInAFileNamedAfterItsClass()
	{
		List<string> offenders = FindDeclarations()
			.Where(predicate: declaration => declaration.FileName != declaration.ClassName)
			.Select(selector: declaration => $"{declaration.ClassName} is in {declaration.FileName}.cs")
			.ToList();

		await Assert.That(value: offenders).IsEmpty().Because(message: $"""
			{String.Join(separator: Environment.NewLine, values: offenders)}

			A configuration nobody can find by name stops being maintained with the others. This is the
			exact shape of the RoleEntityConfigurations slip: the class was named correctly, the file was
			not, and every tool that works by file name skipped it.
		""");
	}

	[Test]
	public async Task EveryConfiguration_ShouldBeNamedAfterItsEntity()
	{
		List<string> offenders = FindDeclarations()
			.Where(predicate: declaration => declaration.ClassName != $"{declaration.EntityName}Configuration")
			.Select(selector: declaration => $"{declaration.ClassName} configures {declaration.EntityName}, expected {declaration.EntityName}Configuration")
			.ToList();

		await Assert.That(value: offenders).IsEmpty().Because(message: $"""
			{String.Join(separator: Environment.NewLine, values: offenders)}

			The file name rule above is only useful while the class name follows from the entity. Break
			this one and a correctly named file still hides what it maps.
		""");
	}

	[Test]
	public async Task NoFile_ShouldHoldMoreThanOneConfiguration()
	{
		List<string> offenders = FindDeclarations()
			.GroupBy(keySelector: declaration => declaration.FilePath)
			.Where(predicate: group => group.Count() > 1)
			.Select(selector: group => $"{Path.GetFileName(path: group.Key)}: {String.Join(separator: ", ", values: group.Select(selector: d => d.ClassName))}")
			.ToList();

		await Assert.That(value: offenders).IsEmpty().Because(message: $"""
			{String.Join(separator: Environment.NewLine, values: offenders)}

			The other half of the same problem: a second configuration tucked into someone else's file is
			as invisible as a misnamed one, and the file name gives no hint that it is there.
		""");
	}
}
