using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;

namespace FinanceTracker.Infrastructure.Database.Repositories.Currency;

public sealed class CurrencyRateWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider
) : ICurrencyRateWriteRepository
{
	public async Task UpsertRatesAsync(
		IReadOnlyList<CurrencyRate> rates,
		CancellationToken ct = default)
	{
		if (rates.Count == 0)
			return;

		await context.UpsertCurrencyRatesAsync(
			baseCodes: rates.Select(selector: r => r.Base.Value).ToArray(),
			targetCodes: rates.Select(selector: r => r.Target.Value).ToArray(),
			rateValues: rates.Select(selector: r => r.Rate).ToArray(),
			actualAtDates: rates.Select(selector: r => r.Date).ToArray(),
			createdAt: dateProvider.UtcNow,
			ct: ct
		);
	}
}