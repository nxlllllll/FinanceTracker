namespace FinanceTracker.Api.Contracts.Auth.Response;

/// <summary>Successful authentication response.</summary>
public sealed record SessionResponse(
	string AccessToken,
	DateTimeOffset AccessTokenExpiresAt
);
