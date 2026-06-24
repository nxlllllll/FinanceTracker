namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Marks a request as email-scoped for rate limiting purposes, for commands that
/// don't yet have an authenticated <see cref="IUserScopedRequest.UserId"/> to key on
/// (e.g. login, registration) but do carry an email address supplied by the caller.
/// </summary>
public interface IEmailScopedRequest
{
	/// <summary>Email address supplied with the request. Used as part of the rate limit key.</summary>
	Core.ValueObjects.Email Email { get; }
}