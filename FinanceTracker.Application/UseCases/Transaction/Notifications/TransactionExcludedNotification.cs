using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications;

public sealed record TransactionExcludedNotification(
	Guid TransactionId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;