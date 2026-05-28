using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record TransactionReadModel(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	decimal ExchangeRate,
	bool IsExcluded,
	bool IsRatePending,
	string? Description,
	DateTimeOffset OccurredAt
) : IReadModel;