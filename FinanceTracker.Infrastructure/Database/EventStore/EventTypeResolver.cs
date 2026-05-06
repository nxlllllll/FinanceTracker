using System.Collections.Frozen;
using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class EventTypeResolver : IEventTypeResolver
{
	private readonly FrozenDictionary<string, Type> _eventTypes;
 
	public EventTypeResolver(
		Assembly assembly,
		ILogger<EventTypeResolver> logger)
	{
		List<Type> eventTypes = assembly.GetTypes().Where(predicate: type => type.IsAssignableTo(targetType: typeof(IEvent)) && type.IsClass).ToList();
 
		List<string> missingAttribute = eventTypes.Where(predicate: type => type.GetCustomAttribute<EventTypeAttribute>() is null)
			.Select(selector: type => type.Name).ToList();

		if (missingAttribute.Count > 0)
		{
			logger.ZLogError(message: $"Configuration error: {String.Join(separator: ", ", missingAttribute)} are missing [EventType] attribute.");
			throw new UnknownEventTypeException(
				message: "The following IEvent classes are missing [EventType] attribute.", 
				eventTypes: missingAttribute
			);
		}
 
		_eventTypes = eventTypes.ToFrozenDictionary(keySelector: type => type.GetCustomAttribute<EventTypeAttribute>()!.Name);
	}
 
	public Type ResolveType(string typeName)
	{
		if (!_eventTypes.TryGetValue(key: typeName, out Type? type))
			throw new UnknownEventTypeException(message: "Unknown event type.", eventTypes: [typeName]);
 
		return type;
	}
}