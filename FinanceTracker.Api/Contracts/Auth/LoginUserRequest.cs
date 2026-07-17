namespace FinanceTracker.Api.Contracts.Auth;

/// <summary>Body of <c>POST /api/v1/auth/login</c>.</summary>
public sealed record LoginUserRequest(
	string Email,
	string Password
);

/// <summary>Successful authentication response.</summary>
public sealed record SessionResponse(
	string AccessToken,
	DateTimeOffset AccessTokenExpiresAt
);
