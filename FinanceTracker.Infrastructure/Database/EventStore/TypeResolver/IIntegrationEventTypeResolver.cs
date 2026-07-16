namespace FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;

/// <summary>
/// Resolves integration event types for the outbox publisher.
/// Provides bidirectional mapping between CLR types and their string discriminators
/// used in the outbox <c>event_type</c> column.
/// </summary>
public interface IIntegrationEventTypeResolver
{
	/// <summary>
	/// Returns the integration event CLR type for the given event type string.
	/// Throws if the type is not registered.
	/// </summary>
	Type ResolveType(string eventType);

	/// <summary>
	/// Returns the event type string discriminator for the given integration event CLR type.
	/// Throws if the type is not registered.
	/// </summary>
	string ResolveTypeName(Type eventType);
}
