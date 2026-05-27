namespace FinanceTracker.Core.Services.Auth;

public sealed record SessionToken(
	string AccessToken,
	string RefreshToken,
	DateTimeOffset AccessTokenExpiresAt
);