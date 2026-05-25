namespace FinanceTracker.Core.Dtos;

public sealed record TokenResponse(
	string AccessToken,
	string RefreshToken,
	DateTimeOffset AccessTokenExpiresAt
);
