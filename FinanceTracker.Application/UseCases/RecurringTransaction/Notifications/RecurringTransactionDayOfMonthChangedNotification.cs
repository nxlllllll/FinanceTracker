using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;

public sealed record RecurringTransactionDayOfMonthChangedNotification(
	Guid RecurringTransactionId,
	Guid UserId,
	int NewDayOfMonth,
	DateTimeOffset OccurredAt
) : INotification;