namespace FinanceTracker.Core.Repositories.User;

public interface IBaseCurrencyRecalculationReadRepository
{
	/// <summary>
	/// Whether this user's category totals are currently unusable because a
	/// base currency change has not been fully applied to them.
	/// </summary>
	Task<bool> TotalsAreUnavailableAsync(Guid userId, CancellationToken ct = default);
}
