using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

/// <summary>
/// Resolves domain event type metadata at runtime.
/// Used by <c>PostgresEventStore</c> to deserialize stored event payloads
/// into the correct <see cref="FinanceTracker.Core.Domains.Abstractions.EventStore.Event.IEvent"/> type.
/// </summary>
public interface IEventTypeResolver
{
	/// <summary>
	/// Returns the CLR type for the given <see cref="EventTypeAttribute.Name"/>.
	/// Throws if the type is not registered.
	/// </summary>
	Type ResolveType(string typeName);

	/// <summary>
	/// Returns the current schema version for the given event type name.
	/// Used to determine whether upcasting is needed when loading from the store.
	/// </summary>
	int GetCurrentVersion(string typeName);
}