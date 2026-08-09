using System.Reflection;
using FinanceTracker.Application.Configurations;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.TransientExceptions;
using FinanceTracker.Tests.Architecture.Helpers;

namespace FinanceTracker.Tests.Architecture;

/// <summary>
/// Every <see cref="AppException"/> exists to be answered with a meaningful status
/// code. The switch in <c>ResultExtensions</c> ends in a catch-all that returns 500,
/// so a type nobody remembered to map quietly starts reporting a server error for
/// something the caller could have acted on.
/// </summary>
public sealed class ErrorMappingArchitectureTests
{
	private const string ApiProject = "FinanceTracker.Api";

	private static readonly Assembly[] SearchedAssemblies =
	[
		typeof(AppException).Assembly,
		typeof(DependencyInjection).Assembly
	];

	private static readonly Type[] BasesWithACatchAllBranch =
	[
		typeof(ValidationException),
		typeof(DomainException),
		typeof(TransientException)
	];

	[Test]
	public async Task EveryAppException_ShouldResolveToSomethingOtherThanFiveHundred()
	{
		string mappingSource = MappingSource();

		List<string> unmapped = ConcreteAppExceptions()
			.Where(predicate: type => !IsCoveredByBase(type: type) && !IsNamedInMapping(type: type, source: mappingSource)).Select(selector: type =>
				$"{type.Name} derives from AppException but ToProblem neither names it nor covers its base " +
				$"({String.Join(separator: ", ", values: BasesWithACatchAllBranch.Select(selector: b => b.Name))}), " +
				"so it falls through to the 500 catch-all. Give it a status code, or move it under a base that already has one."
			).ToList();

		await Assert.That(value: unmapped).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: unmapped));
	}

	[Test]
	public async Task ACurrencyRateGap_ShouldBeTransientRatherThanAConfigurationError()
	{
		await Assert.That(value: typeof(TransientException).IsAssignableFrom(c: typeof(CurrencyRateMissingException))).IsTrue().Because(message: """
			A missing rate is not a broken deployment — the rate job fills the gap on its own, so the
			caller should be told to come back rather than told the server failed. Moving this back
			outside AppException would restore 500s on transfers, base-currency changes and the total
			balance query at once.
		""");
	}

	[Test]
	public async Task TheMapping_ShouldNotPutTypeNamesInTheResponseBody()
	{
		string source = SourceScan.StripComments(source: MappingSource());

		await Assert.That(value: source.Contains(value: "title:", comparisonType: StringComparison.Ordinal)).IsFalse().Because(message: """
			Leaving title to the framework keeps it at the status reason phrase. Setting it from the
			exception type would put an internal identifier in the response, where a rename during
			refactoring changes the contract for anyone parsing it and nothing warns about it.
			What distinguishes failures belongs in code, which is declared per type on purpose.
		""");
	}

	[Test]
	public async Task TheScan_ShouldFindBothKindsOfCoverage()
	{
		await Assert.That(value: ConcreteAppExceptions()).IsNotEmpty()
			.Because(message: "No AppException subtypes were discovered — the assembly list is wrong and this suite would pass without checking anything.");

		await Assert.That(value: IsNamedInMapping(type: typeof(NotFoundException), source: MappingSource())).IsTrue()
			.Because(message: "NotFoundException is named in ToProblem's switch. If the source scan stops seeing it, every type looks unmapped or the file moved.");

		await Assert.That(value: IsCoveredByBase(type: typeof(InsufficientFundsException))).IsTrue()
			.Because(message: "InsufficientFundsException is a plain DomainException covered by the 422 branch. If base coverage stops resolving, the rule turns into noise.");
	}

	private static IEnumerable<Type> ConcreteAppExceptions()
	{
		return SearchedAssemblies.SelectMany(selector: assembly => assembly.GetTypes())
			.Where(predicate: type => type is { IsClass: true, IsAbstract: false } && typeof(AppException).IsAssignableFrom(c: type))
			.Distinct();
	}

	private static bool IsCoveredByBase(Type type)
		=> BasesWithACatchAllBranch.Any(predicate: baseType => baseType.IsAssignableFrom(c: type));

	private static bool IsNamedInMapping(Type type, string source)
		=> source.Contains(value: type.Name, comparisonType: StringComparison.Ordinal);

	private static string MappingSource()
		=> SourceScan.ReadFile(projectName: ApiProject, "Http", "Results", "ResultExtensions.cs");
}
