using MediatR;

namespace FinanceTracker.Application.UseCases.Transaction.Notifications;

public sealed record TransactionCategoryChangedNotification(
	Guid TransactionId,
	Guid UserId,
	Guid OldCategoryId,
	Guid NewCategoryId,
	DateTimeOffset OccurredAt
) : INotification;
