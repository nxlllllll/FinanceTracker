using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Tests.Architecture;

public sealed class EventTypeUniquenessArchitectureTests
{
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;

	private static List<Type> EventClasses()
		=> CoreAssembly.GetTypes().Where(predicate: type => type.IsAssignableTo(targetType: typeof(IEvent)) && type is { IsClass: true, IsAbstract: false }).ToList();

	[Test]
	public async Task EveryEventClass_ShouldDeclareAnEventTypeName()
	{
		List<string> violations = EventClasses().Where(predicate: type => type.GetCustomAttribute<EventTypeAttribute>() is null)
			.Select(selector: type => type.Name)
			.ToList();

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"An event with no [EventType] has no name to be stored under, and the resolver refuses to " +
			$"start without one: {String.Join(separator: ", ", values: violations)}"
		);
	}

	[Test]
	public async Task EventTypeNames_ShouldBeUnique()
	{
		List<string> violations = EventClasses().Where(predicate: type => type.GetCustomAttribute<EventTypeAttribute>() is not null)
			.GroupBy(keySelector: type => type.GetCustomAttribute<EventTypeAttribute>()!.Name)
			.Where(predicate: group => group.Count() > 1)
			.Select(selector: group => $"'{group.Key}' declared by {String.Join(separator: ", ", group.Select(selector: type => type.Name))}")
			.ToList();

		await Assert.That(value: violations).IsEmpty().Because(message:
			$"Two classes cannot share an [EventType] name — the resolver keys stored payloads by it. " +
			$"If one of these is a frozen version kept for upcasting, drop IEvent from it: the attribute " +
			$"alone is what keys the upcaster chain. Conflicts: {String.Join(separator: ", ", values: violations)}"
		);
	}
}
