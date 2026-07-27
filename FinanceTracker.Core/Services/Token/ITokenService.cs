namespace FinanceTracker.Core.Services.Token;

/// <summary>
/// Low-level token generation service used by <c>ISessionIssuer</c>.
/// Responsible for creating JWT access tokens and opaque refresh tokens.
/// </summary>
public interface ITokenService
{
	/// <summary>Generates a signed JWT access token for the given user.</summary>
	AccessTokenResult GenerateAccessToken(
		Domains.User.User user,
		Guid sessionId
	);

	/// <summary>Generates a cryptographically random opaque refresh token string.</summary>
	string GenerateRefreshToken();

	/// <summary>
	/// Hashes a refresh token for secure storage.
	/// Use <c>Verify</c> on the hasher to compare on refresh.
	/// </summary>
	string HashRefreshToken(string refreshToken);

	/// <summary>Returns the absolute expiry <see cref="DateTimeOffset"/> for a new refresh token.</summary>
	DateTimeOffset GetRefreshTokenExpiry();

	/// <summary>
	/// Returns how long after a rotation a replay of the old refresh token is still treated as a
	/// retry rather than reuse of a stolen one. <see cref="TimeSpan.Zero"/> disables the allowance.
	/// </summary>
	TimeSpan GetRefreshReplayGraceWindow();
}
