using System.Text.Json;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Infrastructure.Database.EventStore;

public sealed class EventUpcasterRegistry : IEventUpcasterRegistry
{
	private readonly IReadOnlyDictionary<string, IReadOnlyList<IEventUpcaster>> _chains;

	public EventUpcasterRegistry(
		IEnumerable<IEventUpcaster> upcasters,
		ILogger<EventUpcasterRegistry> logger)
	{
		_chains = upcasters.GroupBy(keySelector: u => u.EventType).ToDictionary(
			keySelector: g => g.Key,
			elementSelector: g => GetChain(logger: logger, g: g)
		);
	}

	private static IReadOnlyList<IEventUpcaster> GetChain(ILogger<EventUpcasterRegistry> logger, IGrouping<string, IEventUpcaster> g)
	{
		IReadOnlyList<IEventUpcaster> chain = g.OrderBy(keySelector: u => u.FromVersion).ToList().AsReadOnly();

		ValidateChain(eventType: g.Key, chain: chain, logger: logger);
		return chain;
	}

	public JsonDocument Apply(
		string eventType,
		JsonDocument source,
		int storedVersion,
		int currentVersion)
	{
		if (storedVersion >= currentVersion || !_chains.TryGetValue(key: eventType, out IReadOnlyList<IEventUpcaster>? chain))
			return source;

		JsonDocument current = source;

		foreach (IEventUpcaster upcaster in chain)
		{
			if (upcaster.FromVersion < storedVersion || upcaster.FromVersion >= currentVersion)
				continue;

			JsonDocument next = upcaster.Upcast(source: current);

			if (!ReferenceEquals(objA: current, objB: source))
				current.Dispose();

			current = next;
		}

		return current;
	}

	private static void ValidateChain(
		string eventType,
		IReadOnlyList<IEventUpcaster> chain,
		ILogger logger)
	{
		for (int i = 0; i < chain.Count - 1; i++)
			if (chain[i].ToVersion != chain[i + 1].FromVersion)
				logger.ZLogWarning(message: $"[Upcasting] Gap in upcaster chain for '{eventType}': v{chain[i].ToVersion} > v{chain[i + 1].FromVersion}.");
	}
}
