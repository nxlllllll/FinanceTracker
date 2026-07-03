using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels;

public sealed record TransferReadModel(
	Guid Id,
	Guid UserId,
	Guid FromAccountId,
	Guid ToAccountId,
	Money AmountFrom,
	Money AmountTo,
	decimal ExchangeRate,
	bool IsRatePending,
	TransferStatus Status,
	string? Description,
	DateTimeOffset OccurredAt
) : IReadModel;
