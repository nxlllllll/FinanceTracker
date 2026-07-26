namespace FinanceTracker.Api.Endpoints.Auth.Contracts;

/// <summary>Body of <c>POST /api/v1/auth/login</c>.</summary>
public sealed record LoginUserRequest(
	string Email,
	string Password
);
