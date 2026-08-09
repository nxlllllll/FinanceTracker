namespace FinanceTracker.Infrastructure.Database.Repositories.User;

public sealed record BaseCurrencyRecalculationClaimDto(
	Guid UserId,
	string TargetCurrency,
	DateTimeOffset RequestedAt,
	int Attempts,
	string? LastError
);
