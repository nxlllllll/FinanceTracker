using System.Collections.Concurrent;
using FinanceTracker.Worker.Shared.RabbitMQ.Connection;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Retry;

public sealed class InMemoryRetryCounter(IOptions<RabbitMqOptions> options) : IRetryCounter
{
	private readonly record struct Entry(int Count, DateTimeOffset AddedAt);

	private readonly ConcurrentDictionary<string, Entry> _counts = new ConcurrentDictionary<string, Entry>();
	private readonly TimeSpan _ttl = TimeSpan.FromHours(value: options.Value.RetryCounterTtlHours);

	public Task<int> IncrementAsync(string messageKey, CancellationToken ct = default)
	{
		PurgeExpired();

		Entry entry = _counts.AddOrUpdate(
			key: messageKey,
			addValueFactory: _ => new Entry(Count: 0, AddedAt: DateTimeOffset.UtcNow),
			updateValueFactory: (_, existing) => existing with { Count = existing.Count + 1 }
		);

		return Task.FromResult(result: entry.Count);
	}

	public Task RemoveAsync(string messageKey, CancellationToken ct = default)
	{
		_counts.TryRemove(key: messageKey, value: out _);
		return Task.CompletedTask;
	}

	private void PurgeExpired()
	{
		DateTimeOffset cutoff = DateTimeOffset.UtcNow - _ttl;

		foreach (string key in _counts.Where(predicate: kv => kv.Value.AddedAt < cutoff).Select(selector: kv => kv.Key))
			_counts.TryRemove(key: key, value: out _);
	}
}