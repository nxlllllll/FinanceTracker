using MediatR;

namespace FinanceTracker.Application.UseCases.Transfer.Notifications;

public sealed record TransferCreatedNotification(
	Guid TransferId,
	Guid UserId,
	Guid FromAccountId,
	Guid ToAccountId,
	decimal AmountFrom,
	string CurrencyFrom,
	decimal AmountTo,
	string CurrencyTo,
	decimal ExchangeRate,
	bool IsRatePending,
	string? Description,
	DateTimeOffset OccurredAt
) : INotification;
