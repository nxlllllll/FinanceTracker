namespace FinanceTracker.Core.Repositories;

public interface ICurrencyRateRepository
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