using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Currency;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.CurrencyRate;

public class CurrencyRateReadRepository(
	FinanceTrackerContext context
) : ICurrencyRateReadRepository
{
	public async Task<decimal?> GetRateAsync(
		string baseCurrencyCode,
		string targetCurrencyCode,
		DateOnly date,
		CancellationToken ct = default)
	{
		if (baseCurrencyCode == targetCurrencyCode)
			return 1m;

		return await context.CurrencyRates.AsNoTracking()
			.Where(predicate: rate =>
				rate.BaseCode == baseCurrencyCode &&
				rate.TargetCode == targetCurrencyCode &&
				rate.ActualAt == date
			).Select(selector: rate => (decimal?)rate.Rate)
			.FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<decimal?> GetLatestRateAsync(
		string baseCurrencyCode,
		string targetCurrencyCode,
		CancellationToken ct = default)
	{
		if (baseCurrencyCode == targetCurrencyCode)
            return 1m;

        return await context.CurrencyRates.AsNoTracking()
			.Where(predicate: rate =>
			    rate.BaseCode == baseCurrencyCode &&
			    rate.TargetCode == targetCurrencyCode
			).OrderByDescending(keySelector: rate => rate.ActualAt)
			.Select(selector: r => (decimal?)r.Rate)
			.FirstOrDefaultAsync(cancellationToken: ct);
	}
}