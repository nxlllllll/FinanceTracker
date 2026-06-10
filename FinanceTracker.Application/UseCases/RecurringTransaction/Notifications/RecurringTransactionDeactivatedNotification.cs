using MediatR;

namespace FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;

public sealed record RecurringTransactionDeactivatedNotification(
	Guid RecurringTransactionId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;