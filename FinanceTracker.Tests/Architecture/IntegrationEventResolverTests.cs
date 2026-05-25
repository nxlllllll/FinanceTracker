using System.Reflection;
using FinanceTracker.Contracts.Events;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Architecture;

public sealed class IntegrationEventResolverTests
{
	private static readonly Assembly ContractsAssembly = typeof(IAccountIntegrationEvent).Assembly;

	private static IReadOnlyList<Type> IntegrationEventTypes => ContractsAssembly.GetTypes()
		.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IAccountIntegrationEvent).IsAssignableFrom(c: t))
		.ToList();

	private static IntegrationEventTypeResolver BuildResolver() => new IntegrationEventTypeResolver(
		contractsAssembly: ContractsAssembly,
		logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
	);

	[Test]
	public async Task IntegrationEventTypeResolver_ShouldBuildWithoutThrowing()
	{
		Exception? exception = null;

		try
		{
			_ = BuildResolver();
		}
		catch (Exception ex)
		{
			exception = ex;
		}

		await Assert.That(value: exception).IsNull()
			.Because(message: $"Resolver failed to build: {exception?.Message}");
	}

	[Test]
	public async Task ResolveType_ShouldResolveAllRegisteredEventNames()
	{
		IntegrationEventTypeResolver resolver = BuildResolver();
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
	public async Task ResolveTypeName_ShouldResolveAllRegisteredTypes()
	{
		IntegrationEventTypeResolver resolver = BuildResolver();
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
	public async Task ResolveType_WithUnknownName_ShouldThrow()
	{
		IntegrationEventTypeResolver resolver = BuildResolver();

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: resolver.ResolveType(eventType: "unknown.event")));
	}

	[Test]
	public async Task ResolveTypeName_WithUnknownType_ShouldThrow()
	{
		IntegrationEventTypeResolver resolver = BuildResolver();

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: resolver.ResolveTypeName(eventType: typeof(object))));
	}
}
