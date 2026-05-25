using FinanceTracker.Core.Dtos;

namespace FinanceTracker.Core.Repositories.Currency;

public interface ICurrencyRateWriteRepository
{
	Task UpsertRatesAsync(
		IReadOnlyList<CurrencyRateDto> rates,
		CancellationToken ct = default
	);
}
