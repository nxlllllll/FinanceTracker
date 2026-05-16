using System.Collections.Frozen;
using System.Reflection;
using FinanceTracker.Contracts.Events;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.ES.Event;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

public sealed class IntegrationEventTypeResolver : IIntegrationEventTypeResolver
{
	private readonly FrozenDictionary<string, Type> _byName;
	private readonly FrozenDictionary<Type, string> _byType;

	public IntegrationEventTypeResolver(
		Assembly contractsAssembly,
		ILogger<IntegrationEventTypeResolver> logger)
	{
		List<Type> integrationEventTypes = contractsAssembly.GetTypes()
			.Where(predicate: t => t is { IsClass: true, IsAbstract: false } && typeof(IAccountIntegrationEvent).IsAssignableFrom(c: t))
			.ToList();

		List<string> missingAttribute = integrationEventTypes.Where(predicate: t => t.GetCustomAttribute<IntegrationEventTypeAttribute>() is null)
			.Select(selector: t => t.Name)
			.ToList();

		if (missingAttribute.Count > 0)
		{
			logger.ZLogError(message: $"Missing [IntegrationEventType] on: {String.Join(", ", missingAttribute)}");
			throw new InvalidOperationException(message: $"Missing [IntegrationEventType] on: {String.Join(", ", missingAttribute)}");
		}

		List<string> missingEventType = integrationEventTypes.Select(selector: t => t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType)
			.Where(predicate: domainType => domainType.GetCustomAttribute<EventTypeAttribute>() is null)
			.Select(selector: domainType => domainType.Name)
			.ToList();

		if (missingEventType.Count > 0)
		{
			logger.ZLogError(message: $"Domain types missing [EventType]: {String.Join(", ", missingEventType)}");
			throw new InvalidOperationException(message: $"Domain types missing [EventType]: {String.Join(", ", missingEventType)}");
		}

		_byName = integrationEventTypes.ToFrozenDictionary(keySelector: GetEventNameFromAttribute);
		_byType = integrationEventTypes.ToFrozenDictionary(keySelector: t => t, elementSelector: GetEventNameFromAttribute);

		logger.ZLogInformation(message: $"Registered {_byName.Count} integration event type(s).");
	}

	private string GetEventNameFromAttribute(Type t)
	{
		Type domainType = t.GetCustomAttribute<IntegrationEventTypeAttribute>()!.DomainEventType;
		return domainType.GetCustomAttribute<EventTypeAttribute>()!.Name;
	}

	public Type ResolveType(string eventType)
	{
		if (!_byName.TryGetValue(key: eventType, value: out Type? type))
			throw new InvalidOperationException(message: $"Unknown integration event type: '{eventType}'.");

		return type;
	}

	public string ResolveTypeName(Type eventType)
	{
		if (!_byType.TryGetValue(key: eventType, value: out string? name))
			throw new InvalidOperationException(message: $"Unregistered integration event type: '{eventType.Name}'.");

		return name;
	}
}