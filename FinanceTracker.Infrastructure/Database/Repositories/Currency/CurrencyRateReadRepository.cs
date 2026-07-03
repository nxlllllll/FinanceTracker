using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Extensions;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Currency;

public sealed class CurrencyRateReadRepository(FinanceTrackerContext context) : ICurrencyRateReadRepository
{
	private const decimal SameCurrencyRate = 1m;

	/// <summary>
	/// Splits <paramref name="requests"/> into same-currency ones (resolved immediately to
	/// <see cref="SameCurrencyRate"/>, no DB lookup needed) and the rest, returned via
	/// <paramref name="foreignRequests"/> for the caller's own DB query. Shared by every batch
	/// method below — they differ only in how they query the foreign ones, not in this split.
	/// </summary>
	private static Dictionary<TRequest, decimal> SplitSameCurrency<TRequest>(
		IReadOnlyCollection<TRequest> requests,
		Func<TRequest, Core.ValueObjects.Currency> from,
		Func<TRequest, Core.ValueObjects.Currency> to,
		out List<TRequest> foreignRequests)
		where TRequest : notnull
	{
		Dictionary<TRequest, decimal> result = [];
		foreignRequests = [];

		foreach (TRequest request in requests)
		{
			if (from(request) == to(request))
				result[request] = SameCurrencyRate;
			else
				foreignRequests.Add(item: request);
		}

		return result;
	}

	public async Task<decimal?> GetRateAsync(
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
		DateOnly date,
		CancellationToken ct = default)
	{
		if (baseCurrencyCode == targetCurrencyCode)
			return SameCurrencyRate;

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
			return SameCurrencyRate;

		return await context.CurrencyRates.AsNoTracking()
			.Where(predicate: rate => rate.BaseCode == baseCurrencyCode && rate.TargetCode == targetCurrencyCode)
			.OrderByDescending(keySelector: rate => rate.ActualAt)
			.Select(selector: r => (decimal?)r.Rate)
			.FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<decimal?> GetRateKnownAtOrBeforeAsync(
		Core.ValueObjects.Currency baseCurrencyCode,
		Core.ValueObjects.Currency targetCurrencyCode,
		DateTimeOffset asOf,
		CancellationToken ct = default)
	{
		if (baseCurrencyCode == targetCurrencyCode)
			return SameCurrencyRate;

		return await context.CurrencyRates.AsNoTracking()
			.Where(predicate: rate => rate.BaseCode == baseCurrencyCode && rate.TargetCode == targetCurrencyCode && rate.CreatedAt <= asOf)
			.OrderByDescending(keySelector: rate => rate.CreatedAt)
			.Select(selector: r => (decimal?)r.Rate)
			.FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<Dictionary<CurrencyLatestRateRequest, decimal>> GetLatestRatesBatchAsync(
		IReadOnlyCollection<CurrencyLatestRateRequest> pairs,
		CancellationToken ct = default)
	{
		if (pairs.Count == 0)
			return [];

		Dictionary<CurrencyLatestRateRequest, decimal> result = SplitSameCurrency(
			requests: pairs,
			from: p => p.From,
			to: p => p.To,
			foreignRequests: out List<CurrencyLatestRateRequest> foreignPairs
		);

		if (foreignPairs.Count == 0)
			return result;

		string[] fromCodes = foreignPairs.Select(selector: p => p.From.Value).Distinct().ToArray();
		string[] toCodes = foreignPairs.Select(selector: p => p.To.Value).Distinct().ToArray();

		Dictionary<(string BaseCode, string TargetCode), decimal> rows = await context.GetLatestCurrencyRatesBatchAsync(
			fromCodes: fromCodes,
			toCodes: toCodes,
			ct: ct
		);

		foreach (CurrencyLatestRateRequest pair in foreignPairs)
			if (rows.TryGetValue(key: (pair.From.Value, pair.To.Value), out decimal rate))
				result[pair] = rate;

		return result;
	}

	public async Task<Dictionary<CurrencyStableRateRequest, decimal>> GetRatesKnownAtOrBeforeBatchAsync(
		IReadOnlyCollection<CurrencyStableRateRequest> requests,
		CancellationToken ct = default)
	{
		if (requests.Count == 0)
			return [];

		Dictionary<CurrencyStableRateRequest, decimal> result = SplitSameCurrency(
			requests: requests,
			from: r => r.From,
			to: r => r.To,
			foreignRequests: out List<CurrencyStableRateRequest> foreignRequests
		);

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
