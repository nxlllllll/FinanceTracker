using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Services.Rebuild;
using FinanceTracker.Infrastructure.Database.Repositories.Account;

namespace FinanceTracker.Tests.Architecture;

public sealed class ProjectionRebuildArchitectureTests
{
	private static readonly Assembly InfrastructureAssembly = typeof(AccountWriteRepository).Assembly;

	private static IReadOnlyList<Type> Implementations => InfrastructureAssembly.GetTypes()
		.Where(predicate: type => type is { IsClass: true, IsAbstract: false } && typeof(IProjectionRebuild).IsAssignableFrom(c: type))
		.ToList();

	private static readonly IReadOnlySet<string> KnownAggregateTypes = typeof(AggregateTypeNames)
		.GetFields(bindingAttr: BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
		.Where(predicate: field => field.IsLiteral)
		.Select(selector: field => (string)field.GetRawConstantValue()!)
		.ToHashSet(comparer: StringComparer.Ordinal);

	[Test]
	public async Task EveryProjectionRebuild_ShouldCarryTheAttributeThatNamesIt()
	{
		IEnumerable<string> missing = Implementations
			.Where(predicate: type => type.GetCustomAttribute<ProjectionAttribute>() is null)
			.Select(selector: type => type.Name);

		await Assert.That(value: missing).IsEmpty().Because(message: """
			Without the attribute the registry cannot name the projection, so it simply is not in the list of
			things that can be rebuilt. Nothing fails — it is just quietly absent, and whoever needs it finds
			out during the incident that made them reach for a rebuild.
		""");
	}

	[Test]
	public async Task ProjectionNames_ShouldBeUnique()
	{
		IEnumerable<string> duplicated = Implementations
			.Select(selector: type => type.GetCustomAttribute<ProjectionAttribute>())
			.Where(predicate: attribute => attribute is not null)
			.GroupBy(keySelector: attribute => attribute!.Name, comparer: StringComparer.OrdinalIgnoreCase)
			.Where(predicate: group => group.Count() > 1)
			.Select(selector: group => group.Key);

		await Assert.That(value: duplicated).IsEmpty().Because(message: """
			Two projections answering to one name means the registry hands back whichever it saw first, and a
			rebuild silently repairs the wrong read model while reporting success for the right one.
		""");
	}

	[Test]
	public async Task ProjectionAggregateTypes_ShouldExistInAggregateTypeNames()
	{
		IEnumerable<string> unknown = Implementations
			.Select(selector: type => type.GetCustomAttribute<ProjectionAttribute>())
			.Where(predicate: attribute => attribute is not null && !KnownAggregateTypes.Contains(item: attribute.AggregateType))
			.Select(selector: attribute => attribute!.AggregateType);

		await Assert.That(value: unknown).IsEmpty().Because(message: """
			The aggregate type is matched against the discriminator stored in the event log. A typo does not
			fail — it selects nothing, and the rebuild reports that every aggregate was processed successfully
			because there were none.
		""");
	}
}
