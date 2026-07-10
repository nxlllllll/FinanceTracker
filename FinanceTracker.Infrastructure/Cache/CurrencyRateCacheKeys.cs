using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Infrastructure.Cache;

internal static class CurrencyRateCacheKeys
{
	public static string LatestRateKey(Currency from, Currency to)
		=> $"rate:latest:{from.Value}:{to.Value}";

	public static string RateKey(CurrencyRateRequest request)
		=> $"rate:{request.From.Value}:{request.To.Value}:{request.Date:yyyyMMdd}";

	public static string StableRateKey(CurrencyStableRateRequest request)
		=> $"rate:stable:{request.From.Value}:{request.To.Value}:{request.AsOf.UtcTicks}";
}
