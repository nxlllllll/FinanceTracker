namespace FinanceTracker.Core.Services.Auth;

/// <summary>
/// Issued by <see cref="ISessionIssuer"/> after successful authentication.
/// Contains a short-lived JWT access token and a long-lived opaque refresh token.
/// <param name="AccessToken">Signed JWT for authenticating API requests.</param>
/// <param name="RefreshToken">Opaque token used to obtain a new access token without re-authentication.</param>
/// <param name="AccessTokenExpiresAt">UTC expiry of the access token.</param>
/// </summary>
public sealed record SessionToken(
	string AccessToken,
	string RefreshToken,
	DateTimeOffset AccessTokenExpiresAt
);
