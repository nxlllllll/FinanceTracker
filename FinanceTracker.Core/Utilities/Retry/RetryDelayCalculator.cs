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
		// Рассеивает retry-волны при высоком параллелизме
		return Jitter.Next(minValue: 0, maxValue: exponential + 1);
	}
}