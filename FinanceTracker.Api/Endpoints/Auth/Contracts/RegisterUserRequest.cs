namespace FinanceTracker.Api.Endpoints.Auth.Contracts;

/// <summary>Body of <c>POST /api/v1/auth/register</c>.</summary>
public sealed record RegisterUserRequest(
	string Email,
	string Password,
	string BaseCurrency,
	string? TimeZone = null
);
