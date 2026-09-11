using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Notifications;

public sealed record TransferCancelledNotification(
	Guid TransferId,
	Guid UserId,
	Guid FromAccountId,
	Guid ToAccountId,
	Guid? ReversalId,
	decimal AmountFrom,
	string CurrencyFrom,
	decimal AmountTo,
	string CurrencyTo,
	bool CreditHadLanded,
	DateTimeOffset OccurredAt
) : INotification;
