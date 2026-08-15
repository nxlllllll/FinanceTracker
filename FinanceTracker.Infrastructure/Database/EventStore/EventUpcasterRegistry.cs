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
			elementSelector: g => BuildChains(eventType: g.Key, sorted: [.. g.OrderBy(keySelector: u => u.FromVersion)])
		);
	}

	private static IReadOnlyDictionary<int, Chain> BuildChains(string eventType, List<IEventUpcaster> sorted)
	{
		for (int i = 0; i < sorted.Count; i++)
		{
			IEventUpcaster current = sorted[i];

			if (current.FromVersion >= current.ToVersion)
			{
				throw new InvalidOperationException(message:
					$"[Upcasting] '{eventType}': {current.GetType().Name} declares [UpcasterVersion(from:" +
					$"{current.FromVersion}, to: {current.ToVersion})], which does not move forward."
				);
			}

			if (i == sorted.Count - 1)
				continue;

			IEventUpcaster next = sorted[i + 1];

			if (current.FromVersion == next.FromVersion)
			{
				throw new InvalidOperationException(
					message: $"[Upcasting] '{eventType}': {current.GetType().Name} and {next.GetType().Name} both upcast from version {current.FromVersion}."
				);
			}

			if (current.ToVersion != next.FromVersion)
			{
				throw new InvalidOperationException(message:
					$"[Upcasting] '{eventType}': nothing upcasts from version {current.ToVersion} to {next.FromVersion}." +
					$"{current.GetType().Name} ends at {current.ToVersion} and the next step starts at {next.FromVersion}," +
					$"so a payload stored before the gap cannot reach the current shape."
				);
			}

			if (current.ToType != next.FromType)
			{
				throw new InvalidOperationException(
					message: $"[Upcasting] '{eventType}': {current.GetType().Name} produces {current.ToType.Name} but {next.GetType().Name} consumes {next.FromType.Name}."
				);
			}
		}

		return sorted.Select(selector: (u, i) => (upcaster: u, index: i)).ToDictionary(
			keySelector: x => x.upcaster.FromVersion,
			elementSelector: x => new Chain(
				upcasters: sorted.GetRange(index: x.index, count: sorted.Count - x.index),
				fromType: x.upcaster.FromType
			)
		);
	}

	public bool HasChain(string eventType) => _chains.ContainsKey(key: eventType);

	public EventUpcasterChain? DescribeChain(string eventType)
	{
		if (!_chains.TryGetValue(key: eventType, out IReadOnlyDictionary<int, Chain>? versionedChains) || versionedChains.Count == 0)
			return null;

		IReadOnlyList<IEventUpcaster> longest = versionedChains.Values.OrderBy(keySelector: chain => chain.Upcasters[0].FromVersion).First().Upcasters;

		return new EventUpcasterChain(
			FromVersion: longest[0].FromVersion,
			ToVersion: longest[^1].ToVersion
		);
	}

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
			if (upcaster.FromVersion >= currentVersion)
			{
				throw new InvalidOperationException(message:
					$"[Upcasting] '{eventType}': the chain from version {storedVersion} reaches {upcaster.FromVersion}, " +
					$"but this build only knows version {currentVersion}. The stored schema is ahead of the code."
				);
			}

			current = upcaster.Upcast(source: current);
		}

		return (IEvent)current;
	}
}
