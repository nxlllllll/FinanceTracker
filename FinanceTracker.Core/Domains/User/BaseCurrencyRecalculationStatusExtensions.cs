namespace FinanceTracker.Core.Domains.User;

public static class BaseCurrencyRecalculationStatusExtensions
{
	/// <summary>
	/// Whether totals should be withheld from reads. True until a rebuild finishes, and also when it
	/// has been abandoned — amounts still denominated in the previous currency are not slightly out
	/// of date, they are a different order of magnitude.
	/// </summary>
	public static bool TotalsAreUnavailable(this BaseCurrencyRecalculationStatus status)
		=> status is not BaseCurrencyRecalculationStatus.Completed;
}
