namespace FinanceTracker.Core.Repositories.Currency;

public interface ICurrencyRateReadRepository
{
	Task<decimal?> GetRateAsync(
		string baseCurrencyCode,
		string targetCurrencyCode,
		DateOnly date,
		CancellationToken ct = default
	);

	Task<decimal?> GetLatestRateAsync(
		string baseCurrencyCode,
		string targetCurrencyCode,
		CancellationToken ct = default
	);
}