namespace FinanceTracker.Core.Services.Token;

/// <summary>
/// Result of <see cref="ITokenService.GenerateAccessToken"/>.
/// Contains the signed JWT string and its expiry for inclusion in the session record.
/// <param name="Token">The signed JWT access token string.</param>
/// <param name="ExpiresAt">UTC expiry of the token.</param>
/// </summary>
public sealed record AccessTokenResult( 
	string Token,
	DateTimeOffset ExpiresAt
);