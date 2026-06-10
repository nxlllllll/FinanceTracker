using System.Diagnostics;
using FinanceTracker.Core.Exceptions.DomainExceptions;

namespace FinanceTracker.Core.Utilities.Retry;

/// <summary>
/// Calculates retry delays using exponential backoff with optional full jitter,
/// and provides a generic retry execution helper.
/// Used by <c>ConcurrencyRetryBehavior</c> and <c>IdempotencyBehavior</c>.
/// </summary>
public static class RetryDelayCalculator
{
	private static readonly Random Jitter = Random.Shared;

	/// <summary>
	/// Calculates a delay for the given retry <paramref name="attempt"/>.
	/// Formula: <c>baseDelayMs * 2^attempt</c> with optional full jitter (<c>[0, exponential]</c>).
	/// </summary>
	/// <param name="attempt">Zero-based attempt index.</param>
	/// <param name="baseDelayMs">Base delay in milliseconds.</param>
	/// <param name="useJitter">When <c>true</c>, randomises the delay to spread concurrent retries.</param>
	public static int Calculate(int attempt, int baseDelayMs, bool useJitter)
	{
		int exponential = baseDelayMs * (1 << attempt);

		if (!useJitter)
			return exponential;
		
		return Jitter.Next(minValue: 0, maxValue: exponential + 1);
	}

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
			logging: (ex, attempt, delay) => logging((ConcurrencyConflictException)ex, attempt, delay),
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
		Action<Exception, int, int> logging,
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
			logging: logging,
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
		Action<Exception, int, int> logging,
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
				logging(exception, attempt, delayMs);
				await Task.Delay(millisecondsDelay: delayMs, cancellationToken: ct);
			}
		}

		throw new UnreachableException();
	}
}