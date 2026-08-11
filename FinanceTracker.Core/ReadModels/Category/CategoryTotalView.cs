namespace FinanceTracker.Core.ReadModels.Category;

/// <summary>One category's total for a period.</summary>
public sealed record CategoryTotalView(
	CategoryTotal? Total,
	bool RecalculationPending
) : IReadModel
{
	public static CategoryTotalView Pending()
		=> new CategoryTotalView(Total: null, RecalculationPending: true);

	public static CategoryTotalView Ready(CategoryTotal total)
		=> new CategoryTotalView(Total: total, RecalculationPending: false);
}
