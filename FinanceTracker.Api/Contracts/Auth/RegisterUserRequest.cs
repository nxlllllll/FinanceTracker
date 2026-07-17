namespace FinanceTracker.Api.Contracts.Auth;

/// <summary>Body of <c>POST /api/v1/auth/register</c>.</summary>
public sealed record RegisterUserRequest(
	string Email,
	string Password,
	string BaseCurrency
);
