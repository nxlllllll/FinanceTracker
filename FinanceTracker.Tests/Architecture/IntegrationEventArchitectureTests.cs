using System.Reflection;
using FinanceTracker.Contracts.Events;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Infrastructure.Database.EventStore.EventMapper;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Architecture;

public sealed class IntegrationEventArchitectureTests
{
	private static readonly Assembly ContractsAssembly = typeof(IAccountIntegrationEvent).Assembly;
	private static readonly Assembly CoreAssembly = typeof(IEvent).Assembly;

	private static IReadOnlyList<Type> IntegrationEventTypes => ContractsAssembly.GetTypes()
		.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IAccountIntegrationEvent).IsAssignableFrom(c: t))
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
			.Because(message: $"Missing [IntegrationEventType] on: {String.Join(", ", violations)}");
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
			.Because(message: $"[IntegrationEventType] references non-IEvent types: {String.Join(", ", violations)}");
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
			.Because(message: $"Domain types referenced in [IntegrationEventType] are missing [EventType]: {String.Join(", ", violations)}");
	}

	[Test]
	public async Task AllIntegrationEventTypes_ShouldHaveUniqueEventTypeNames()
	{
		List<string> names = IntegrationEventTypes.Select(selector: t =>
		{
			Type domainType = t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType;
			return domainType.GetCustomAttribute<EventTypeAttribute>()!.Name;
		}).ToList();

		List<string> duplicates = names.GroupBy(keySelector: n => n)
			.Where(predicate: g => g.Count() > 1)
			.Select(selector: g => g.Key)
			.ToList();

		await Assert.That(value: duplicates).IsEmpty()
			.Because(message: $"Duplicate integration event type names: {String.Join(", ", duplicates)}");
	}

	[Test]
	public async Task AccountIntegrationEventMapper_ShouldMapAllAccountDomainEvents()
	{
		AccountIntegrationEventMapper mapper = new AccountIntegrationEventMapper();

		List<string> mappedDomainTypes = IntegrationEventTypes.Select(selector: t => t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType.Name).ToList();

		List<string> unmapped = AccountDomainEventTypes.Where(predicate: t => !mappedDomainTypes.Contains(value: t.Name))
			.Select(selector: t => t.Name)
			.ToList();

		await Assert.That(value: unmapped).IsEmpty()
			.Because(message: $"Account domain events with no integration event mapping: {String.Join(", ", unmapped)}");
	}

	[Test]
	public async Task IntegrationEventTypeResolver_ShouldBuildWithoutThrowingOnValidContracts()
	{
		Exception? exception = null;

		try
		{
			_ = new IntegrationEventTypeResolver(
				contractsAssembly: ContractsAssembly,
				logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
			);
		}
		catch (Exception ex)
		{
			exception = ex;
		}

		await Assert.That(value: exception).IsNull()
			.Because(message: $"Resolver failed to build: {exception?.Message}");
	}

	[Test]
	public async Task IntegrationEventTypeResolver_ResolveType_ShouldResolveAllRegisteredNames()
	{
		IntegrationEventTypeResolver resolver = new IntegrationEventTypeResolver(
			contractsAssembly: ContractsAssembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		);

		List<string> failures = [];

		foreach (Type integrationEventType in IntegrationEventTypes)
		{
			Type domainType = integrationEventType.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType;
			string eventTypeName = domainType.GetCustomAttribute<EventTypeAttribute>()!.Name;

			try
			{
				Type resolved = resolver.ResolveType(eventType: eventTypeName);
				if (resolved != integrationEventType)
					failures.Add(item: $"{eventTypeName} resolved to {resolved.Name} instead of {integrationEventType.Name}");
			}
			catch (Exception ex)
			{
				failures.Add(item: $"{eventTypeName}: {ex.Message}");
			}
		}

		await Assert.That(value: failures).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: failures));
	}

	[Test]
	public async Task IntegrationEventTypeResolver_ResolveTypeName_ShouldResolveAllRegisteredTypes()
	{
		IntegrationEventTypeResolver resolver = new IntegrationEventTypeResolver(
			contractsAssembly: ContractsAssembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		);

		List<string> failures = [];

		foreach (Type integrationEventType in IntegrationEventTypes)
		{
			Type domainType = integrationEventType.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType;
			string expectedName = domainType.GetCustomAttribute<EventTypeAttribute>()!.Name;

			try
			{
				string resolved = resolver.ResolveTypeName(eventType: integrationEventType);
				if (resolved != expectedName)
					failures.Add(item: $"{integrationEventType.Name} resolved to '{resolved}' instead of '{expectedName}'");
			}
			catch (Exception ex)
			{
				failures.Add(item: $"{integrationEventType.Name}: {ex.Message}");
			}
		}

		await Assert.That(value: failures).IsEmpty()
			.Because(message: String.Join(separator: "\n", values: failures));
	}

	[Test]
	public async Task IntegrationEventTypeResolver_ResolveType_WithUnknownName_ShouldThrow()
	{
		IntegrationEventTypeResolver resolver = new IntegrationEventTypeResolver(
			contractsAssembly: ContractsAssembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		);

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: resolver.ResolveType(eventType: "unknown.event")));
	}

	[Test]
	public async Task IntegrationEventTypeResolver_ResolveTypeName_WithUnknownType_ShouldThrow()
	{
		IntegrationEventTypeResolver resolver = new IntegrationEventTypeResolver(
			contractsAssembly: ContractsAssembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		);

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: resolver.ResolveTypeName(eventType: typeof(Object))));
	}
}