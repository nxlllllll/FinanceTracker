using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;

public sealed record RecurringTransactionAmountChangedNotification(
	Guid RecurringTransactionId,
	Guid UserId,
	decimal NewAmount,
	DateTimeOffset OccurredAt
) : INotification;