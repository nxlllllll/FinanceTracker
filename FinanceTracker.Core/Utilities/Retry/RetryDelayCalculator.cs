using System.Diagnostics;
using FinanceTracker.Core.Exceptions.DomainExceptions;

namespace FinanceTracker.Core.Utilities.Retry;

public static class RetryDelayCalculator
{
	private static readonly Random Jitter = Random.Shared;

	public static int Calculate(int attempt, int baseDelayMs, bool useJitter)
	{
		// Exponential backoff: baseDelayMs * 2^(attempt)
		int exponential = baseDelayMs * (1 << attempt);

		if (!useJitter)
			return exponential;
		
		// Full jitter: random in [0, exponential]
		return Jitter.Next(minValue: 0, maxValue: exponential + 1);
	}
	
	public static async Task<T> ExecuteWithRetryAsync<T>(
		Func<CancellationToken, Task<T>> operation,
		Action<ConcurrencyConflictException, int, int> logging,
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
			catch (ConcurrencyConflictException exception) when (attempt < maxRetries)
			{
				int delayMs = Calculate(attempt: attempt, baseDelayMs: baseDelayMs, useJitter: useJitter);
				logging(exception, attempt, delayMs);
				await Task.Delay(millisecondsDelay: delayMs, cancellationToken: ct);
			}
		}

		throw new UnreachableException();
	}
}
