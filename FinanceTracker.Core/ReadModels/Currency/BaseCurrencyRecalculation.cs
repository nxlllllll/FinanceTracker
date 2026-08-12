using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Core.ReadModels.Currency;

/// <summary>A pending or finished rebuild of one user's category totals.</summary>
public sealed record BaseCurrencyRecalculation(
	Guid UserId,
	BaseCurrencyRecalculationStatus Status,
	ValueObjects.Currency TargetCurrency,
	DateTimeOffset RequestedAt,
	int Attempts,
	string? LastError
);
