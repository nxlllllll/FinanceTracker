namespace FinanceTracker.Api.Endpoints.Users.Contracts;

public sealed record ChangeEmailRequest(
	string CurrentPassword,
	string NewEmail
);
