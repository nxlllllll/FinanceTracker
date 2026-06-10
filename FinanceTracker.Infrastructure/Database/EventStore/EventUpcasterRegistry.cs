using System.Collections.Frozen;
using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class EventUpcasterRegistry : IEventUpcasterRegistry
{
	private sealed class Chain(IReadOnlyList<IEventUpcaster> upcasters, Type fromType)
	{
		public IReadOnlyList<IEventUpcaster> Upcasters { get; } = upcasters;
		public Type FromType => fromType;
	}

	private readonly FrozenDictionary<string, IReadOnlyDictionary<int, Chain>> _chains;

	public EventUpcasterRegistry(IEnumerable<IEventUpcaster> upcasters)
	{
		_chains = upcasters.GroupBy(keySelector: u => u.EventType).ToFrozenDictionary(
			keySelector: g => g.Key,
			elementSelector: g => BuildChains(sorted: [..g.OrderBy(keySelector: u => u.FromVersion)])
		);
	}

	private static IReadOnlyDictionary<int, Chain> BuildChains(List<IEventUpcaster> sorted)
	{
		return sorted.Select(selector: (u, i) => (upcaster: u, index: i)).ToDictionary(
			keySelector: x => x.upcaster.FromVersion,
			elementSelector: x => new Chain(
				upcasters: sorted.GetRange(index: x.index, count: sorted.Count - x.index),
				fromType: x.upcaster.FromType
			)
		);
	}

	public bool HasChain(string eventType) => _chains.ContainsKey(key: eventType);

	public IEvent Apply(
		string eventType,
		string payload,
		int storedVersion,
		int currentVersion)
	{
		if (!_chains.TryGetValue(key: eventType, out IReadOnlyDictionary<int, Chain>? versionedChains))
			throw new InvalidOperationException(message: $"[Upcasting] No chain found for event type '{eventType}'.");

		if (!versionedChains.TryGetValue(key: storedVersion, out Chain? chain))
			throw new InvalidOperationException(message: $"[Upcasting] No upcaster found for '{eventType}' from version {storedVersion}.");

		object current = JsonSerializer.Deserialize(json: payload, returnType: chain.FromType, options: FinanceTrackerJsonOptions.Payload)!;

		foreach (IEventUpcaster upcaster in chain.Upcasters)
		{
			if (upcaster.FromVersion < storedVersion || upcaster.FromVersion >= currentVersion)
				continue;

			current = upcaster.Upcast(source: current);
		}

		return (IEvent)current;
	}
}