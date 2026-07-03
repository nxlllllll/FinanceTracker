using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Repositories.Currency;

public interface ICurrencyRateWriteRepository
{
	Task UpsertRatesAsync(
		IReadOnlyList<CurrencyRate> rates,
		CancellationToken ct = default
	);
}
