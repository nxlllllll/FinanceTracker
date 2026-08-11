using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.ReadModels.Transfer;

public sealed record TransferReadModel(
	Guid Id,
	Guid UserId,
	Guid FromAccountId,
	Guid ToAccountId,
	Money AmountFrom,
	Money AmountTo,
	decimal ExchangeRate,
	RateStatus RateStatus,
	TransferStatus Status,
	string? Description,
	DateTimeOffset OccurredAt
) : IReadModel;
