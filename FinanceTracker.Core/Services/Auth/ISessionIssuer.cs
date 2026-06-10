namespace FinanceTracker.Core.Services.Auth;

/// <summary>
/// Creates a new authenticated session for a user, issuing both an access token
/// and a refresh token, and persisting the session in the store.
/// </summary>
public interface ISessionIssuer
{
	/// <summary>
	/// Issues a <see cref="SessionToken"/> containing a short-lived JWT access token
	/// and a long-lived refresh token. The refresh token is hashed before storage.
	/// </summary>
	Task<SessionToken> IssueAsync(
		Domains.User.User user,
		CancellationToken ct = default
	);
}