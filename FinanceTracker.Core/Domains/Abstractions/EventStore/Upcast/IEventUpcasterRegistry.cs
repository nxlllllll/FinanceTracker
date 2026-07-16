using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

/// <summary>
/// Applies the registered upcaster chain for a given event type, returning a fully
/// migrated <see cref="IEvent"/> without intermediate re-serialization.
/// </summary>
public interface IEventUpcasterRegistry
{
	/// <summary>
	/// Deserializes <paramref name="payload"/> into the first upcaster's <c>TFrom</c>,
	/// applies all applicable upcasters in version order, and returns the final <see cref="IEvent"/>.
	/// Only call when <see cref="HasChain"/> returns <c>true</c> and
	/// <paramref name="storedVersion"/> is less than <paramref name="currentVersion"/>.
	/// </summary>
	IEvent Apply(
		string eventType,
		string payload,
		int storedVersion,
		int currentVersion
	);

	/// <summary>
	/// Returns <c>true</c> if at least one upcaster is registered for the given event type.
	/// Use this to decide whether to call <see cref="Apply"/> or deserialize directly.
	/// </summary>
	bool HasChain(string eventType);
}
