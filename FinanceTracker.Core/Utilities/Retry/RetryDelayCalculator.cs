using System.Diagnostics;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;

namespace FinanceTracker.Core.Utilities.Retry;

/// <summary>
/// Calculates retry delays using exponential backoff with optional full jitter,
/// and provides a generic retry execution helper.
/// Used by <c>ConcurrencyRetryBehavior</c> and <c>IdempotencyBehavior</c>.
/// </summary>
public static class RetryDelayCalculator
{
	private static readonly Random Jitter = Random.Shared;
	private const int MaxShift = 16;

	/// <summary>
	/// <c>2^attempt</c>, saturating at <see cref="MaxShift"/>. Safe for any attempt count.
	/// </summary>
	private static int Exponential(int attempt)
		=> 1 << Math.Clamp(value: attempt, min: 0, max: MaxShift);

	/// <summary>
	/// Calculates a delay for the given retry <paramref name="attempt"/>.
	/// Formula: <c>baseDelayMs * 2^attempt</c> with optional full jitter (<c>[0, exponential]</c>).
	/// </summary>
	/// <param name="attempt">Zero-based attempt index.</param>
	/// <param name="baseDelayMs">Base delay in milliseconds.</param>
	/// <param name="useJitter">When <c>true</c>, randomises the delay to spread concurrent retries.</param>
	public static int Calculate(int attempt, int baseDelayMs, bool useJitter)
	{
		int exponential = (int)Math.Min(
			val1: (long)baseDelayMs * Exponential(attempt: attempt),
			val2: Int32.MaxValue - 1
		);

		if (!useJitter)
			return exponential;

		return Jitter.Next(minValue: 0, maxValue: exponential + 1);
	}

	/// <summary>
	/// Exponential backoff in whole seconds, capped at <paramref name="maxSeconds"/>. For reconnect
	/// loops, where the attempt counter is unbounded by design.
	/// </summary>
	public static int CalculateSeconds(int attempt, int maxSeconds)
		=> Math.Min(val1: maxSeconds, val2: Exponential(attempt: attempt));

	/// <summary>
	/// Executes <paramref name="operation"/> with automatic retry on <see cref="ConcurrencyConflictException"/>.
	/// </summary>
	public static async Task<T> ExecuteWithRetryAsync<T>(
		Func<CancellationToken, Task<T>> operation,
		Action<ConcurrencyConflictException, int, int> logging,
		int maxRetries,
		int baseDelayMs,
		bool useJitter,
		CancellationToken ct)
	{
		return await ExecuteWithRetryAsync(
			operation: operation,
			onError: (ex, attempt, delay) => logging((ConcurrencyConflictException)ex, attempt, delay),
			exceptionFilter: ex => ex is ConcurrencyConflictException,
			maxRetries: maxRetries,
			baseDelayMs: baseDelayMs,
			useJitter: useJitter,
			ct: ct
		);
	}

	/// <summary>
	/// Executes <paramref name="operation"/> with automatic retry for any exception
	/// matching <paramref name="exceptionFilter"/>.
	/// </summary>
	public static async Task ExecuteWithRetryAsync(
		Func<CancellationToken, Task> operation,
		Action<Exception, int, int> onError,
		Func<Exception, bool> exceptionFilter,
		int maxRetries,
		int baseDelayMs,
		bool useJitter,
		CancellationToken ct)
	{
		await ExecuteWithRetryAsync(
			operation: async innerCt =>
			{
				await operation(innerCt);
				return true;
			},
			onError: onError,
			exceptionFilter: exceptionFilter,
			maxRetries: maxRetries,
			baseDelayMs: baseDelayMs,
			useJitter: useJitter,
			ct: ct
		);
	}

	/// <summary>
	/// Core retry loop. Retries up to <paramref name="maxRetries"/> times,
	/// waiting <see cref="Calculate"/> milliseconds between attempts.
	/// Re-throws the last exception if all retries are exhausted.
	/// </summary>
	public static async Task<T> ExecuteWithRetryAsync<T>(
		Func<CancellationToken, Task<T>> operation,
		Action<Exception, int, int> onError,
		Func<Exception, bool> exceptionFilter,
		int maxRetries,
		int baseDelayMs,
		bool useJitter,
		CancellationToken ct)
	{
		for (int attempt = 0; attempt <= maxRetries; attempt++)
		{
			try
			{
				return await operation(ct);
			}
			catch (Exception exception) when (exceptionFilter(exception) && attempt < maxRetries)
			{
				int delayMs = Calculate(attempt: attempt, baseDelayMs: baseDelayMs, useJitter: useJitter);
				onError(exception, attempt, delayMs);
				await Task.Delay(millisecondsDelay: delayMs, cancellationToken: ct);
			}
		}

		throw new UnreachableException();
	}
}
