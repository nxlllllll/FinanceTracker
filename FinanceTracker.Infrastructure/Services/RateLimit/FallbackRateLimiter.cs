using FinanceTracker.Core.Services.Metrics;
using FinanceTracker.Core.Services.RateLimit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services.RateLimit;

/// <summary>
/// Decorator for <see cref="IRateLimiter"/> that degrades to <see cref="InMemoryRateLimiter"/>
/// when the Redis-backed limiter is unreachable or too slow, instead of failing open (no limit)
/// or failing closed (blocking the request entirely).
/// </summary>
public sealed class FallbackRateLimiter(
	IRateLimiter inner,
	InMemoryRateLimiter fallback,
	IOptionsMonitor<RateLimiterFallbackOptions> options,
	ILogger<FallbackRateLimiter> logger
) : IRateLimiter
{
	private int _isDegraded;

	public async Task<RateLimitResult> IsAllowedAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct = default)
	{
		Task<RateLimitResult> primaryTask = inner.IsAllowedAsync(
			key: key,
			requestsPerWindow: requestsPerWindow,
			windowSeconds: windowSeconds,
			ct: ct
		);

		using CancellationTokenSource timeoutCts = new CancellationTokenSource();
		Task delayTask = Task.Delay(delay: TimeSpan.FromMilliseconds(value: options.CurrentValue.ProbeTimeoutMs), cancellationToken: timeoutCts.Token);

		try
		{
			Task completed = await Task.WhenAny(task1: primaryTask, task2: delayTask);

			if (completed == primaryTask)
			{
				RateLimitResult result = await primaryTask;
				await timeoutCts.CancelAsync();
				MarkRedisReachable();
				return result;
			}

			MarkDegraded(reason: $"Redis probe exceeded {options.CurrentValue.ProbeTimeoutMs}ms.");
			ObserveDelayedFailure(primaryTask);
		}
		catch (RedisException ex)
		{
			MarkDegraded(reason: ex.Message);
		}

		return await fallback.IsAllowedAsync(key: key, requestsPerWindow: requestsPerWindow, windowSeconds: windowSeconds, ct: ct);
	}

	private void MarkRedisReachable()
	{
		if (Interlocked.Exchange(ref _isDegraded, 0) != 1)
			return;

		fallback.Clear();
		logger.ZLogInformation(message: $"[RateLimiter] Redis reachable again — cleared in-memory fallback state.");
	}

	private void MarkDegraded(string reason)
	{
		FinanceTrackerMetrics.RateLimiterFallbackActivated.Add(delta: 1);

		if (Interlocked.Exchange(ref _isDegraded, 1) == 0)
			logger.ZLogWarning(message: $"[RateLimiter] Falling back to in-memory rate limiting — {reason}");
	}

	/// <summary>
	/// The Redis call that lost the timeout race keeps running in the background — this
	/// observes its eventual outcome so a late failure is logged instead of becoming an
	/// unobserved task exception, without making the caller wait for it.
	/// </summary>
	private void ObserveDelayedFailure(Task<RateLimitResult> task)
	{
		_ = task.ContinueWith(
			continuationAction: t => logger.ZLogWarning(
				exception: t.Exception!.GetBaseException(),
				message: $"[RateLimiter] Delayed Redis probe failed after timeout."
			),
			continuationOptions: TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously
		);
	}
}
