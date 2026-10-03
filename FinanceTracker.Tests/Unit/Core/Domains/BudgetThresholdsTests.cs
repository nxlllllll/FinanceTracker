using FinanceTracker.Core.Domains.Budget;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class BudgetThresholdsTests
{
	private const decimal Limit = 1000m;
	private static readonly int[] Thresholds = [80, 100];

	private static int? Spend(decimal from, decimal to) => BudgetThresholds.GetHighestCrossed(
		spentBefore: from,
		limitBefore: Limit,
		spentAfter: to,
		limitAfter: Limit,
		thresholds: Thresholds
	);

	[Test]
	public async Task ReachingAThresholdExactlyCrossesIt()
	{
		await Assert.That(value: Spend(from: 700m, to: 800m)).IsEqualTo(expected: 80)
			.Because(message: "a budget at exactly 80% of its limit has reached 80%, so waiting for one more kopeck would be late");
	}

	[Test]
	public async Task StoppingJustShortOfAThresholdCrossesNothing()
	{
		await Assert.That(value: Spend(from: 700m, to: 799.99m)).IsNull();
	}

	[Test]
	public async Task SpendingPastAThresholdAlreadyReachedDoesNotCrossItAgain()
	{
		await Assert.That(value: Spend(from: 800m, to: 950m)).IsNull().Because(message: """
			The owner has already been told about 80%. Every purchase after it would otherwise send the same
			notification again until the next threshold.
		""");
	}

	[Test]
	public async Task JumpingPastSeveralThresholdsReportsOnlyTheHighest()
	{
		await Assert.That(value: Spend(from: 700m, to: 1100m)).IsEqualTo(expected: 100).Because(message: """
			One purchase that goes from 70% straight past the limit is one event. Two messages arriving
			together, the first saying 80%, describe a state the budget was never in.
		""");
	}

	[Test]
	public async Task ThresholdsAreComparedRegardlessOfTheOrderTheyAreListedIn()
	{
		int? crossed = BudgetThresholds.GetHighestCrossed(
			spentBefore: 700m,
			limitBefore: Limit,
			spentAfter: 1100m,
			limitAfter: Limit,
			thresholds: [100, 80]
		);

		await Assert.That(value: crossed).IsEqualTo(expected: 100);
	}

	[Test]
	public async Task ARefundCrossesNothing()
	{
		await Assert.That(value: Spend(from: 900m, to: 700m)).IsNull();
	}

	[Test]
	public async Task ClimbingBackPastAThresholdAfterARefundCrossesItAgain()
	{
		await Assert.That(value: Spend(from: 850m, to: 700m)).IsNull();

		await Assert.That(value: Spend(from: 700m, to: 850m)).IsEqualTo(expected: 80).Because(message: """
			Once a cancellation takes the budget back under 80%, reaching it again is new information for
			the owner, so it is reported every time rather than once per period.
		""");
	}

	[Test]
	public async Task NoChangeCrossesNothing()
	{
		await Assert.That(value: Spend(from: 850m, to: 850m)).IsNull();
	}

	[Test]
	public async Task LoweringTheLimitCanCrossAThresholdWithoutAnyNewSpending()
	{
		int? crossed = BudgetThresholds.GetHighestCrossed(
			spentBefore: 700m,
			limitBefore: 1000m,
			spentAfter: 700m,
			limitAfter: 800m,
			thresholds: Thresholds
		);

		await Assert.That(value: crossed).IsEqualTo(expected: 80).Because(message: """
			700 of 1000 is 70%, 700 of 800 is 87.5%. The owner is now past 80% of what they may spend,
			and it does not matter to them that the limit moved rather than the spending.
		""");
	}

	[Test]
	public async Task RaisingTheLimitCrossesNothing()
	{
		int? crossed = BudgetThresholds.GetHighestCrossed(
			spentBefore: 900m,
			limitBefore: 1000m,
			spentAfter: 900m,
			limitAfter: 2000m,
			thresholds: Thresholds
		);

		await Assert.That(value: crossed).IsNull();
	}
}
