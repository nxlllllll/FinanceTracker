using System.Collections.Concurrent;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.RateLimit;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Services.RateLimit;

/// <summary>
/// In-process sliding-window rate limiter used as a fallback by <see cref="FallbackRateLimiter"/>
/// when the Redis-backed limiter is unavailable.
/// </summary>
public sealed class InMemoryRateLimiter(
	IDateProvider dateProvider,
	IOptionsMonitor<InMemoryRateLimiterOptions> options
) : IRateLimiter
{
	private sealed class TrackedWindow
	{
		public readonly Queue<long> Timestamps = new Queue<long>();
		public long LastTouchedMs;
	}

	private readonly ConcurrentDictionary<string, TrackedWindow> _windows = new ConcurrentDictionary<string, TrackedWindow>();
	private long _callCounter;

	public Task<RateLimitResult> IsAllowedAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct = default)
	{
		long now = dateProvider.UtcNow.ToUnixTimeMilliseconds();

		SweepIdleKeysIfDue(now: now);

		if (!_windows.TryGetValue(key: key, value: out TrackedWindow? window))
		{
			EvictLeastRecentlyUsedIfFull(maxTrackedKeys: options.CurrentValue.MaxTrackedKeys);

			window = _windows.GetOrAdd(key: key, valueFactory: _ => new TrackedWindow { LastTouchedMs = now });
		}

		long windowStart = now - windowSeconds * 1000L;

		// Locking on the window instance itself keeps the check-and-increment atomic per key
		// without a separate lock-object table; this is the only place that touches the window.
		lock (window)
		{
			window.LastTouchedMs = now;

			while (window.Timestamps.Count > 0 && window.Timestamps.Peek() < windowStart)
				window.Timestamps.Dequeue();

			if (window.Timestamps.Count >= requestsPerWindow)
			{
				long retryAfterMs = window.Timestamps.Peek() + windowSeconds * 1000L - now;
				int retryAfterSeconds = (int)Math.Ceiling(a: Math.Max(val1: retryAfterMs, val2: 0) / 1000.0);
				return Task.FromResult(result: RateLimitResult.Denied(retryAfterSeconds: retryAfterSeconds));
			}

			window.Timestamps.Enqueue(item: now);
			return Task.FromResult(result: RateLimitResult.Allowed());
		}
	}

	/// <summary>
	/// Trims the table back under the cap by dropping the least recently used windows,
	/// rather than refusing the key that did not fit.
	/// </summary>
	private void EvictLeastRecentlyUsedIfFull(int maxTrackedKeys)
	{
		if (_windows.Count < maxTrackedKeys)
			return;

		int excess = _windows.Count - maxTrackedKeys + 1;

		List<KeyValuePair<string, TrackedWindow>> oldest =
		[
			.._windows.OrderBy(keySelector: entry => entry.Value.LastTouchedMs).Take(count: excess)
		];

		foreach (KeyValuePair<string, TrackedWindow> entry in oldest)
			(_windows as ICollection<KeyValuePair<string, TrackedWindow>>).Remove(item: entry);
	}

	/// <summary>Periodically drops keys nobody has touched in a while</summary>
	private void SweepIdleKeysIfDue(long now)
	{
		if (Interlocked.Increment(location: ref _callCounter) % options.CurrentValue.SweepIntervalCalls != 0)
			return;

		long idleCutoff = now - options.CurrentValue.KeyIdleTimeoutMs;

		foreach (KeyValuePair<string, TrackedWindow> entry in _windows)
		{
			bool isIdle;
			lock (entry.Value)
				isIdle = entry.Value.LastTouchedMs < idleCutoff;

			if (isIdle)
				((ICollection<KeyValuePair<string, TrackedWindow>>)_windows).Remove(item: entry);
		}
	}

	/// <summary>
	/// Discards all tracked windows. Call this once Redis is confirmed reachable again —
	/// there is no reason to keep in-memory state around once the source of truth is back.
	/// </summary>
	public void Clear()
		=> _windows.Clear();
}
