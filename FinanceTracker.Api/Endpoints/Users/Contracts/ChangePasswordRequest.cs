namespace FinanceTracker.Api.Endpoints.Users.Contracts;

public sealed record ChangePasswordRequest(
	string CurrentPassword,
	string NewPassword
);
