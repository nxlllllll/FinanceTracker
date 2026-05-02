using System.Collections.Frozen;
using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class EventTypeResolver : IEventTypeResolver
{
	private readonly FrozenDictionary<string, Type> _eventTypes;

	public EventTypeResolver(Assembly assembly)
	{
		_eventTypes = assembly.GetTypes().Where(predicate: type => type.IsAssignableTo(targetType: typeof(IEvent)) && type.IsClass)
    		.ToFrozenDictionary(keySelector: type => type.GetCustomAttribute<EventTypeAttribute>()?.Name ?? type.Name);
	}

	public Type ResolveType(string typeName)
	{
		if (!_eventTypes.TryGetValue(key: typeName, out Type? type))
			throw new UnknownEventTypeException(message: "Unknown event type.", eventType: typeName);

		return type;
	}
}