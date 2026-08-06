using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

/// <summary>A pending or finished rebuild of one user's category totals.</summary>
public sealed record BaseCurrencyRecalculation(
	Guid UserId,
	BaseCurrencyRecalculationStatus Status,
	Currency TargetCurrency,
	DateTimeOffset RequestedAt,
	int Attempts,
	string? LastError
);
