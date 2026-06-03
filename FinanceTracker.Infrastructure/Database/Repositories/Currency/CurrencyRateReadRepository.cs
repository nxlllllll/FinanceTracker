using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Currency;

public sealed class CurrencyRateReadRepository(FinanceTrackerContext context) : ICurrencyRateReadRepository
{
	public async Task<decimal?> GetRateAsync(
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
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
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
		CancellationToken ct = default)
	{
		if (baseCurrencyCode == targetCurrencyCode)
            return 1m;

        return await context.CurrencyRates.AsNoTracking()
			.Where(predicate: rate => rate.BaseCode == baseCurrencyCode && rate.TargetCode == targetCurrencyCode)
			.OrderByDescending(keySelector: rate => rate.ActualAt)
			.Select(selector: r => (decimal?)r.Rate)
			.FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<Dictionary<CurrencyRateRequest, decimal>> GetRatesBatchAsync(
		IReadOnlyCollection<CurrencyRateRequest> requests,
		CancellationToken ct = default)
	{
		if (requests.Count == 0)
			return [];

		Dictionary<CurrencyRateRequest, decimal> result = [];
		HashSet<CurrencyRateRequest> foreignRequests = [];

		foreach (CurrencyRateRequest request in requests)
		{
			if (request.From == request.To)
				result[request] = 1m;
			else
				foreignRequests.Add(item: request);
		}

		if (foreignRequests.Count == 0)
			return result;

		HashSet<Core.ValueObjects.Currency> fromSet = foreignRequests.Select(selector: r => r.From).ToHashSet();
		HashSet<Core.ValueObjects.Currency> toSet = foreignRequests.Select(selector: r => r.To).ToHashSet();
		HashSet<DateOnly> dateSet = foreignRequests.Select(selector: r => r.Date).ToHashSet();

		Dictionary<CurrencyRateRequest, decimal> dbLookup = await context.CurrencyRates.AsNoTracking()
			.Where(predicate: rate => fromSet.Contains(rate.BaseCode) && toSet.Contains(rate.TargetCode) && dateSet.Contains(rate.ActualAt))
			.ToDictionaryAsync(
				keySelector: rate => new CurrencyRateRequest(From: rate.BaseCode, To: rate.TargetCode, Date: rate.ActualAt),
				elementSelector: rate => rate.Rate,
				cancellationToken: ct
			);

		foreach (CurrencyRateRequest request in foreignRequests)
			if (dbLookup.TryGetValue(key: request, out decimal rate))
				result[request] = rate;

		return result;
	}
}