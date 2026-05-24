namespace FinanceTracker.Core.Services.RateLimit;

public interface IRateLimiter
{
	Task<bool> IsAllowedAsync(
		string key,
		int requestsPerWindow,
		int windowSeconds,
		CancellationToken ct = default
	);
}