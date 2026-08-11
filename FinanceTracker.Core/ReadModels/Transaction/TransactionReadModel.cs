using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels.Transaction;

public sealed record TransactionReadModel(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	Guid CategoryId,
	Money Amount,
	DirectionType Direction,
	decimal ExchangeRate,
	bool IsExcluded,
	RateStatus RateStatus,
	string? Description,
	DateTimeOffset OccurredAt
) : IReadModel;
