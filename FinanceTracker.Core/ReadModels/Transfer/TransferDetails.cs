using FinanceTracker.Core.Domains.Transfer;

namespace FinanceTracker.Core.ReadModels.Transfer;

public sealed record TransferDetails(
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	ValueObjects.Currency CurrencyFrom,
	decimal AmountTo,
	ValueObjects.Currency CurrencyTo,
	TransferStatus Status
) : IReadModel;
