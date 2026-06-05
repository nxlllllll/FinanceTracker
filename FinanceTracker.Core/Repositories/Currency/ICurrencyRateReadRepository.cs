using FinanceTracker.Core.Services.Currency;

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

	Task<Dictionary<CurrencyRateRequest, decimal>> GetRatesBatchAsync(
		IReadOnlyCollection<CurrencyRateRequest> requests,
		CancellationToken ct = default
	);

	Task<Dictionary<CurrencyLatestRateRequest, decimal>> GetLatestRatesBatchAsync(
		IReadOnlyCollection<CurrencyLatestRateRequest> pairs,
		CancellationToken ct = default
	);
}