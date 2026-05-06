namespace FinanceTracker.Core.Repositories.Currency;

public interface ICurrencyRateReadRepository
{
	Task<decimal?> GetRateAsync(
		ValueObjects.Currency baseCurrencyCode,
		ValueObjects.Currency targetCurrencyCode,
		DateOnly date,
		CancellationToken ct = default
	);

	Task<decimal?> GetLatestRateAsync(
		ValueObjects.Currency baseCurrencyCode,
		ValueObjects.Currency targetCurrencyCode,
		CancellationToken ct = default
	);
}