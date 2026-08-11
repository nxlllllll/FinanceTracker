using FinanceTracker.Core.Utilities.Retry;

namespace FinanceTracker.Tests.Unit.Core.Utilities;

public sealed class RetryDelayCalculatorTests
{
	[Test]
	[Arguments(0)]
	[Arguments(1)]
	[Arguments(15)]
	[Arguments(16)]
	[Arguments(30)]
	[Arguments(31)]
	[Arguments(32)]
	[Arguments(64)]
	[Arguments(1_000)]
	[Arguments(Int32.MaxValue)]
	public async Task CalculateSeconds_ForAnyAttempt_ShouldStayWithinBounds(int attempt)
	{
		const int maxSeconds = 30;

		int delay = RetryDelayCalculator.CalculateSeconds(attempt: attempt, maxSeconds: maxSeconds);

		await Assert.That(value: delay).IsGreaterThan(minimum: 0).Because(message: """
			A non-positive delay throws from Task.Delay, and these callers compute it inside their own
			catch block — so the exception escapes the loop and takes the worker down with it.
		""");
		await Assert.That(value: delay).IsLessThanOrEqualTo(maximum: maxSeconds);
	}

	[Test]
	public async Task CalculateSeconds_ShouldGrowUntilItReachesTheCap()
	{
		await Assert.That(value: RetryDelayCalculator.CalculateSeconds(attempt: 0, maxSeconds: 30)).IsEqualTo(expected: 1);
		await Assert.That(value: RetryDelayCalculator.CalculateSeconds(attempt: 2, maxSeconds: 30)).IsEqualTo(expected: 4);
		await Assert.That(value: RetryDelayCalculator.CalculateSeconds(attempt: 4, maxSeconds: 30)).IsEqualTo(expected: 16);
		await Assert.That(value: RetryDelayCalculator.CalculateSeconds(attempt: 10, maxSeconds: 30)).IsEqualTo(expected: 30);
	}

	[Test]
	[Arguments(0)]
	[Arguments(10)]
	[Arguments(31)]
	[Arguments(50)]
	[Arguments(Int32.MaxValue)]
	public async Task Calculate_ForAnyAttempt_ShouldNeverReturnANegativeDelay(int attempt)
	{
		int withoutJitter = RetryDelayCalculator.Calculate(attempt: attempt, baseDelayMs: 100, useJitter: false);
		int withJitter = RetryDelayCalculator.Calculate(attempt: attempt, baseDelayMs: 100, useJitter: true);

		await Assert.That(value: withoutJitter).IsGreaterThanOrEqualTo(minimum: 0);
		await Assert.That(value: withJitter).IsGreaterThanOrEqualTo(minimum: 0);
	}
}
