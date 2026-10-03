namespace FinanceTracker.Core.Domains.Budget;

/// <summary>
/// Decides which spending level, in percent of the limit, a change to a budget has just crossed
/// upwards. Covers both a change to the amount spent and a change to the limit itself.
/// </summary>
public static class BudgetThresholds
{
	/// <summary>
	/// Returns the highest threshold the budget has reached after the change
	/// but had not reached before it, or <c>null</c> if none was crossed.
	/// </summary>
	public static int? GetHighestCrossed(
		decimal spentBefore,
		decimal limitBefore,
		decimal spentAfter,
		decimal limitAfter,
		IReadOnlyCollection<int> thresholds)
	{
		IEnumerable<int> crossed = thresholds.Where(predicate: threshold =>
			!Reaches(spent: spentBefore, limit: limitBefore, threshold: threshold)
			&& Reaches(spent: spentAfter, limit: limitAfter, threshold: threshold)
		);

		return crossed.Any() ? crossed.Max() : null;
	}

	private static bool Reaches(
		decimal spent,
		decimal limit,
		int threshold
	) => spent * 100 >= limit * threshold;
}
