using FinanceTracker.Core.Observability.Metrics;
using FinanceTracker.Core.Services.RateLimit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
	private int _isProbing;
	private long _nextProbeTicks;

	public bool IsDegraded => Volatile.Read(location: ref _isDegraded) == 1;

	public async Task<RateLimitResult> IsAllowedAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct = default)
	{
		if (Volatile.Read(location: ref _isDegraded) == 1)
		{
			TryScheduleRecoveryProbe(
				key: key,
				requestsPerWindow: requestsPerWindow,
				windowSeconds: windowSeconds
			);
			return await ServeFromFallbackAsync(
				key: key,
				requestsPerWindow: requestsPerWindow,
				windowSeconds: windowSeconds,
				ct: ct
			);
		}

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
			ObserveDelayedFailure(task: primaryTask);
		}
		catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
		{
			MarkDegraded(reason: ex.Message);
		}

		return await ServeFromFallbackAsync(
			key: key,
			requestsPerWindow: requestsPerWindow,
			windowSeconds: windowSeconds,
			ct: ct
		);
	}

	private async Task<RateLimitResult> ServeFromFallbackAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct)
	{
		FinanceTrackerMetrics.RateLimiterFallbackActivated.Add(delta: 1);

		return await fallback.IsAllowedAsync(
			key: key,
			requestsPerWindow: requestsPerWindow,
			windowSeconds: windowSeconds,
			ct: ct
		);
	}

	/// <summary>
	/// Starts one Redis probe in the background if the interval has elapsed and no probe is already
	/// running. The caller does not wait for it.
	/// </summary>
	private void TryScheduleRecoveryProbe(string key, int requestsPerWindow, int windowSeconds)
	{
		if (Environment.TickCount64 < Volatile.Read(location: ref _nextProbeTicks))
			return;

		if (Interlocked.Exchange(ref _isProbing, 1) == 1)
			return;

		Volatile.Write(location: ref _nextProbeTicks, value: Environment.TickCount64 + options.CurrentValue.RecoveryProbeIntervalMs);

		_ = ProbeRedisAsync(
			key: key,
			requestsPerWindow: requestsPerWindow,
			windowSeconds: windowSeconds
		);
	}

	private async Task ProbeRedisAsync(string key, int requestsPerWindow, int windowSeconds)
	{
		try
		{
			using CancellationTokenSource probeCts = new CancellationTokenSource(delay: TimeSpan.FromMilliseconds(value: options.CurrentValue.ProbeTimeoutMs));

			await inner.IsAllowedAsync(
				key: key,
				requestsPerWindow: requestsPerWindow,
				windowSeconds: windowSeconds,
				ct: probeCts.Token
			);

			MarkRedisReachable();
		}
		catch (Exception ex)
		{
			logger.ZLogDebug(exception: ex, message: $"[RateLimiter] Recovery probe failed — staying on the in-memory limiter.");
		}
		finally
		{
			Volatile.Write(location: ref _isProbing, value: 0);
		}
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
		if (Interlocked.Exchange(ref _isDegraded, 1) == 1)
			return;

		Volatile.Write(location: ref _nextProbeTicks, value: Environment.TickCount64 + options.CurrentValue.RecoveryProbeIntervalMs);
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
