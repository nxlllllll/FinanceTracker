using System.Net;

namespace FinanceTracker.Application.Behaviours.RateLimit;

/// <summary>
/// Marks a request as IP-scoped for rate limiting purposes, for commands that run
/// before authentication (login, registration, token refresh/revocation) and therefore
/// have no <see cref="IUserScopedRequest.UserId"/> to key on yet.
/// </summary>
public interface IIpScopedRequest
{
	/// <summary>IP address the request originated from. Used as part of the rate limit key.</summary>
	IPAddress IpAddress { get; }
}