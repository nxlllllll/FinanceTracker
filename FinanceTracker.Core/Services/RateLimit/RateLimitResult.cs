namespace FinanceTracker.Core.Services.RateLimit;

public readonly record struct RateLimitResult(bool IsAllowed, int RetryAfterSeconds)
{
	public static RateLimitResult Allowed() => new RateLimitResult(
		IsAllowed: true,
		RetryAfterSeconds: 0
	);

	public static RateLimitResult Denied(int retryAfterSeconds) => new RateLimitResult(
		IsAllowed: false,
		RetryAfterSeconds: retryAfterSeconds
	);
}
