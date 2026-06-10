using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;

namespace FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

public interface IEventUpcasterRegistry
{
	/// <summary>
	/// Deserializes the payload into TFrom of the first applicable upcaster,
	/// applies the chain, and returns the final IEvent without re-serializing.
	/// Only called when storedVersion &lt; currentVersion.
	/// </summary>
	IEvent Apply(
		string eventType,
		string payload,
		int storedVersion,
		int currentVersion
	);

	bool HasChain(string eventType);
}