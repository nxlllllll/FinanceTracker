using System.Collections.Concurrent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.RateLimit;

namespace FinanceTracker.Infrastructure.Services.RateLimit;

/// <summary>
/// In-process sliding-window rate limiter used as a fallback by <see cref="FallbackRateLimiter"/>
/// when the Redis-backed limiter is unavailable.
/// </summary>
public sealed class InMemoryRateLimiter(IDateProvider dateProvider) : IRateLimiter
{
	private readonly ConcurrentDictionary<string, Queue<long>> _windows = new ConcurrentDictionary<string, Queue<long>>();

	public Task<bool> IsAllowedAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct = default)
	{
		Queue<long> window = _windows.GetOrAdd(key: key, valueFactory: _ => new Queue<long>());

		long now = dateProvider.UtcNow.ToUnixTimeMilliseconds();
		long windowStart = now - windowSeconds * 1000L;

		// Locking on the queue instance itself keeps the check-and-increment atomic per key
		// without a separate lock-object table; this is the only place that touches the queue.
		lock (window)
		{
			while (window.Count > 0 && window.Peek() < windowStart)
				window.Dequeue();

			if (window.Count >= requestsPerWindow)
				return Task.FromResult(result: false);

			window.Enqueue(item: now);
			return Task.FromResult(result: true);
		}
	}

	/// <summary>
	/// Discards all tracked windows. Call this once Redis is confirmed reachable again —
	/// there is no reason to keep in-memory state around once the source of truth is back.
	/// </summary>
	public void Clear()
		=> _windows.Clear();
}
