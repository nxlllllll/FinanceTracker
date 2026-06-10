using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;

public sealed record RecurringTransactionActivatedNotification(
	Guid RecurringTransactionId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;