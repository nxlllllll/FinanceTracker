using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
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
		IEnumerable<Type> upcasterTypes = InfrastructureAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && IsEventUpcasterSubclass(type: t));

		Dictionary<string, List<(int From, int To)>> chains = new Dictionary<string, List<(int From, int To)>>();

		foreach (Type upcasterType in upcasterTypes)
		{
			UpcasterVersionAttribute attr = (UpcasterVersionAttribute?)Attribute.GetCustomAttribute(
				element: upcasterType,
				attributeType: typeof(UpcasterVersionAttribute)
			) ?? throw new InvalidOperationException(message: $"{upcasterType.Name} is missing [UpcasterVersion].");

			string eventType = GetEventTypeFromUpcaster(upcasterType: upcasterType);

			if (!chains.TryGetValue(key: eventType, out List<(int, int)>? list))
			{
				list = [];
				chains[eventType] = list;
			}
			list.Add(item: (attr.From, attr.To));
		}

		List<string> gaps = [];

		foreach ((string eventType, List<(int From, int To)> chain) in chains)
		{
			List<(int From, int To)> sorted = chain.OrderBy(keySelector: x => x.From).ToList();
			for (int i = 0; i < sorted.Count - 1; i++)
				if (sorted[i].To != sorted[i + 1].From)
					gaps.Add(item: $"'{eventType}': gap between v{sorted[i].To} and v{sorted[i + 1].From}");
		}

		await Assert.That(value: gaps).IsEmpty()
			.Because(message: $"Upcaster chain gaps detected:\n{String.Join(separator: "\n", values: gaps)}");
	}

	private static bool IsEventUpcasterSubclass(Type type)
	{
		Type? current = type.BaseType;
		while (current is not null)
		{
			if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EventUpcaster<,>))
				return true;

			current = current.BaseType;
		}
		return false;
	}

	private static string GetEventTypeFromUpcaster(Type upcasterType)
	{
		Type? current = upcasterType.BaseType;
		while (current is not null)
		{
			if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(EventUpcaster<,>))
			{
				Type tFrom = current.GetGenericArguments()[0];
				EventTypeAttribute? attr = (EventTypeAttribute?)Attribute.GetCustomAttribute(
					element: tFrom,
					attributeType: typeof(EventTypeAttribute)
				);
				return attr?.Name ?? throw new InvalidOperationException(message: $"{tFrom.Name} is missing [EventType].");
			}
			current = current.BaseType;
		}
		throw new InvalidOperationException(message: $"Cannot find EventUpcaster<,> base for {upcasterType.Name}.");
	}
}
