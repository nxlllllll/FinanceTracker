using System.Collections.Concurrent;

namespace FinanceTracker.Worker.Shared.RabbitMQ.Retry;

public sealed class InMemoryRetryCounter : IRetryCounter
{
	private readonly ConcurrentDictionary<string, int> _counts = new ConcurrentDictionary<string, int>();

	public Task<int> IncrementAsync(string messageKey, CancellationToken ct = default)
	{
		int count = _counts.AddOrUpdate(
			key: messageKey,
			addValue: 0,
			updateValueFactory: (_, current) => current + 1
		);
		return Task.FromResult(result: count);
	}

	public Task RemoveAsync(string messageKey, CancellationToken ct = default)
	{
		_counts.TryRemove(key: messageKey, value: out _);
		return Task.CompletedTask;
	}
}