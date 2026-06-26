using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
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

	public async Task<Dictionary<CurrencyLatestRateRequest, decimal>> GetLatestRatesBatchAsync(
		IReadOnlyCollection<CurrencyLatestRateRequest> pairs,
		CancellationToken ct = default)
	{
		if (pairs.Count == 0)
			return [];

		string[] fromCodes = pairs.Select(selector: p => p.From.Value).Distinct().ToArray();
		string[] toCodes = pairs.Select(selector: p => p.To.Value).Distinct().ToArray();

		Dictionary<(string BaseCode, string TargetCode), decimal> rows = await context.GetLatestCurrencyRatesBatchAsync(
			fromCodes: fromCodes,
			toCodes: toCodes,
			ct: ct
		);

		return rows.ToDictionary(
			keySelector: kvp => new CurrencyLatestRateRequest(
				From: Core.ValueObjects.Currency.Reconstitute(value: kvp.Key.BaseCode),
				To: Core.ValueObjects.Currency.Reconstitute(value: kvp.Key.TargetCode)
			),
			elementSelector: kvp => kvp.Value
		);
	}

	public async Task<decimal?> GetRateKnownAtOrBeforeAsync(
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
		DateTimeOffset asOf,
		CancellationToken ct = default)
	{
		if (baseCurrencyCode == targetCurrencyCode)
			return 1m;

		return await context.CurrencyRates.AsNoTracking()
			.Where(predicate: rate => rate.BaseCode == baseCurrencyCode && rate.TargetCode == targetCurrencyCode && rate.CreatedAt <= asOf)
			.OrderByDescending(keySelector: rate => rate.CreatedAt)
			.Select(selector: r => (decimal?)r.Rate)
			.FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<Dictionary<CurrencyStableRateRequest, decimal>> GetRatesKnownAtOrBeforeBatchAsync(
		IReadOnlyCollection<CurrencyStableRateRequest> requests,
		CancellationToken ct = default)
	{
		if (requests.Count == 0)
			return [];

		Dictionary<CurrencyStableRateRequest, decimal> result = [];
		List<CurrencyStableRateRequest> foreignRequests = [];

		foreach (CurrencyStableRateRequest request in requests)
		{
			if (request.From == request.To)
				result[request] = 1m;
			else
				foreignRequests.Add(item: request);
		}

		if (foreignRequests.Count == 0)
			return result;

		List<CurrencyStableRateRequest> distinctRequests = foreignRequests.Distinct().ToList();

		string[] fromCodes = distinctRequests.Select(selector: r => r.From.Value).ToArray();
		string[] toCodes = distinctRequests.Select(selector: r => r.To.Value).ToArray();
		DateTime[] asOfUtc = distinctRequests.Select(selector: r => r.AsOf.UtcDateTime).ToArray();

		Dictionary<(string BaseCode, string TargetCode, DateTime AsOfUtc), decimal> rows = await context.GetCurrencyRatesKnownAtOrBeforeBatchAsync(
			fromCodes: fromCodes,
			toCodes: toCodes,
			asOfUtc: asOfUtc,
			ct: ct
		);

		foreach (CurrencyStableRateRequest request in distinctRequests)
			if (rows.TryGetValue(key: (request.From.Value, request.To.Value, request.AsOf.UtcDateTime), out decimal rate))
				result[request] = rate;

		return result;
	}
}