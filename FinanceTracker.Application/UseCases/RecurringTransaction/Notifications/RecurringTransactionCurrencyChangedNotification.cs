using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;

public sealed record RecurringTransactionCurrencyChangedNotification(
	Guid RecurringTransactionId,
	Guid UserId,
	Core.ValueObjects.Currency NewCurrency,
	DateTimeOffset OccurredAt
) : INotification;