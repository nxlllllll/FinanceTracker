namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Marks a request as user-scoped for rate limiting purposes.
/// Requests implementing this interface are intercepted by
/// <c>RateLimitingBehavior</c>, which enforces per-user request limits
/// using a sliding-window counter in Redis.
/// </summary>
public interface IUserScopedRequest
{
	/// <summary>ID of the user making the request. Used as part of the rate limit key.</summary>
	Guid UserId { get; }
}