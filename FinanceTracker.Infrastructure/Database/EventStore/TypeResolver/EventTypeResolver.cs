using System.Collections.Frozen;
using System.Reflection;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

public sealed class EventTypeResolver : IEventTypeResolver
{
	private readonly FrozenDictionary<string, Type> _eventTypes;
	private readonly FrozenDictionary<string, int> _eventVersions;

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
			throw new UnknownEventTypeException(message: "The following IEvent classes are missing [EventType] attribute.", eventTypes: missingAttribute);
		}

		List<string> duplicates = eventTypes.GroupBy(keySelector: type => type.GetCustomAttribute<EventTypeAttribute>()!.Name)
			.Where(predicate: group => group.Count() > 1)
			.Select(selector: group => $"'{group.Key}' declared by {String.Join(separator: ", ", group.Select(selector: type => type.Name))}")
			.ToList();

		if (duplicates.Count > 0)
		{
			logger.ZLogError(message: $"Configuration error: duplicate [EventType] names. {String.Join(separator: "; ", duplicates)}.");
			throw new DuplicateEventTypeException(
				message: "The following [EventType] names are declared more than once. A frozen event version kept for upcasting" +
						 "must not implement IEvent — its [EventType] attribute is enough to key the upcaster chain.",
				eventTypes: duplicates
			);
		}

		_eventTypes = eventTypes.ToFrozenDictionary(keySelector: type => type.GetCustomAttribute<EventTypeAttribute>()!.Name);

		_eventVersions = eventTypes.ToFrozenDictionary(
			keySelector: type => type.GetCustomAttribute<EventTypeAttribute>()!.Name,
			elementSelector: type => type.GetCustomAttribute<EventVersionAttribute>()?.Version ?? 1
		);
	}

	public Type ResolveType(string typeName)
	{
		if (!_eventTypes.TryGetValue(key: typeName, out Type? type))
			throw new UnknownEventTypeException(message: "Unknown event type.", eventTypes: [typeName]);

		return type;
	}

	public int GetCurrentVersion(string typeName)
		=> _eventVersions.GetValueOrDefault(key: typeName, defaultValue: 1);
}
