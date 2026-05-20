using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Core.Domains.Abstractions.ES.Upcast;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace FinanceTracker.Tests.Architecture;

public sealed class EventSourcingArchitectureTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;
	private static readonly Assembly InfrastructureAssembly = typeof(EFUnitOfWork).Assembly;

	[Test]
	public async Task AllIEventClasses_ShouldHaveEventTypeAttribute()
	{
		TestResult result = Types.InAssembly(assembly: CoreAssembly)
			.That().ImplementInterface(interfaceType: typeof(IEvent))
			.And().AreClasses()
			.Should().HaveCustomAttribute(attribute: typeof(EventTypeAttribute))
			.GetResult();

		await Assert.That(value: result.IsSuccessful).IsTrue()
			.Because(message: String.Join(separator: ", ", values: result.FailingTypes?.Select(t => t.Name) ?? []));
	}

	[Test]
	public async Task AllEventUpcasterChains_ShouldHaveNoGaps()
	{
		IEnumerable<IEventUpcaster> upcasters = InfrastructureAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IEventUpcaster).IsAssignableFrom(c: t))
			.Select(selector: t => (IEventUpcaster)Activator.CreateInstance(type: t)!)
			.ToList();

		Dictionary<string, List<IEventUpcaster>> chains = upcasters
			.GroupBy(keySelector: u => u.EventType)
			.ToDictionary(
				keySelector: g => g.Key,
				elementSelector: g => g.OrderBy(keySelector: u => u.FromVersion).ToList()
			);

		List<string> gaps = [];

		foreach ((string eventType, List<IEventUpcaster> chain) in chains)
			for (int i = 0; i < chain.Count - 1; i++)
				if (chain[i].ToVersion != chain[i + 1].FromVersion)
					gaps.Add(item: $"'{eventType}': gap between v{chain[i].ToVersion} and v{chain[i + 1].FromVersion}");

		await Assert.That(value: gaps).IsEmpty()
				.Because(message: $"Upcaster chain gaps detected:\n{String.Join(separator: "\n", values: gaps)}");
	}
}