using System.Collections.Frozen;
using FinanceTracker.Core.Domains.Abstractions;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class EventTypeRegistry : IEventTypeRegistry
{
	private readonly FrozenDictionary<string, Type> _eventTypes;

	public EventTypeRegistry()
	{
		_eventTypes = typeof(IEvent).Assembly.GetTypes()
			.Where(predicate: type => type.IsAssignableTo(targetType: typeof(IEvent)) && type.IsClass)
			.ToFrozenDictionary(keySelector: type => type.Name);
	}
	
	public Type ResolveType(string typeName)
	{
		if (!_eventTypes.TryGetValue(key: typeName, out Type? type))
			throw new InvalidOperationException(message: $"Unknown event type: {typeName}");
		
		return type;
	}
}