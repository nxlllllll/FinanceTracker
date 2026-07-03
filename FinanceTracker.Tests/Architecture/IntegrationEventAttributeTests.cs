using System.Reflection;
using FinanceTracker.Contracts.Events;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Tests.Architecture;

public sealed class IntegrationEventAttributeTests
{
	private static readonly Assembly ContractsAssembly = typeof(IIntegrationEvent).Assembly;
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;

	private static IReadOnlyList<Type> IntegrationEventTypes => ContractsAssembly.GetTypes()
		.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IIntegrationEvent).IsAssignableFrom(c: t))
		.ToList();

	private static IReadOnlyList<Type> AccountDomainEventTypes => CoreAssembly.GetTypes()
		.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IEvent).IsAssignableFrom(c: t) && t.Namespace?.Contains(value: "Account") == true)
		.ToList();

	[Test]
	public async Task AllIAccountIntegrationEvents_ShouldHaveIntegrationEventTypeAttribute()
	{
		List<string> violations = IntegrationEventTypes
			.Where(predicate: t => t.GetCustomAttribute<IntegrationEventTypeAttribute>() is null)
			.Select(selector: t => t.Name)
			.ToList();

		await Assert.That(value: violations).IsEmpty()
			.Because(message: $"Missing [IntegrationEventType] on: {String.Join(separator: ", ", values: violations)}");
	}

	[Test]
	public async Task AllIntegrationEventTypeAttributes_ShouldReferenceExistingDomainEventType()
	{
		List<string> violations = IntegrationEventTypes
			.Select(selector: t => t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType)
			.Where(predicate: domainType => !typeof(IEvent).IsAssignableFrom(c: domainType))
			.Select(selector: t => t.Name)
			.ToList();

		await Assert.That(value: violations).IsEmpty()
			.Because(message: $"[IntegrationEventType] references non-IEvent types: {String.Join(separator: ", ", values: violations)}");
	}

	[Test]
	public async Task AllIntegrationEventTypeAttributes_ShouldReferenceTypesWithEventTypeAttribute()
	{
		List<string> violations = IntegrationEventTypes
			.Select(selector: t => t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType)
			.Where(predicate: domainType => domainType.GetCustomAttribute<EventTypeAttribute>() is null)
			.Select(selector: t => t.Name)
			.ToList();

		await Assert.That(value: violations).IsEmpty()
			.Because(message: $"Domain types referenced in [IntegrationEventType] are missing [EventType]: {String.Join(separator: ", ", values: violations)}");
	}

	[Test]
	public async Task AllIntegrationEventTypes_ShouldHaveUniqueEventTypeNames()
	{
		List<string> names = IntegrationEventTypes.Select(selector: t =>
		{
			Type domainType = t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType;
			return domainType.GetCustomAttribute<EventTypeAttribute>()!.Name;
		}).ToList();

		List<string> duplicates = names
			.GroupBy(keySelector: n => n)
			.Where(predicate: g => g.Count() > 1)
			.Select(selector: g => g.Key)
			.ToList();

		await Assert.That(value: duplicates).IsEmpty()
			.Because(message: $"Duplicate integration event type names: {String.Join(separator: ", ", values: duplicates)}");
	}

	[Test]
	public async Task AccountIntegrationEventMapper_ShouldMapAllAccountDomainEvents()
	{
		List<string> mappedDomainTypes = IntegrationEventTypes
			.Select(selector: t => t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType.Name)
			.ToList();

		List<string> unmapped = AccountDomainEventTypes
			.Where(predicate: t => !mappedDomainTypes.Contains(value: t.Name))
			.Select(selector: t => t.Name)
			.ToList();

		await Assert.That(value: unmapped).IsEmpty()
			.Because(message: $"Account domain events with no integration event mapping: {String.Join(separator: ", ", values: unmapped)}");
	}
}
