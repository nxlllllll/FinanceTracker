using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications;

public sealed record TransactionIncludedNotification(
	Guid TransactionId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;