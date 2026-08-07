namespace FinanceTracker.Core.ReadModels;

/// <summary>
/// Category totals for a period, together with whether they can be trusted right now.
/// </summary>
public sealed record CategoryTotalsView(
	IReadOnlyList<CategoryTotal> Totals,
	bool RecalculationPending
) : IReadModel
{
	public static CategoryTotalsView Pending()
		=> new CategoryTotalsView(Totals: [], RecalculationPending: true);

	public static CategoryTotalsView Ready(IReadOnlyList<CategoryTotal> totals)
		=> new CategoryTotalsView(Totals: totals, RecalculationPending: false);
}
