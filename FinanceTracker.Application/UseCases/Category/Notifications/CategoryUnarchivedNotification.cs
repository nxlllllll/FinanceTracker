using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Notifications;

public sealed record CategoryUnarchivedNotification(
	Guid CategoryId,
	Guid UserId,
	DateTimeOffset OccurredAt
) : INotification;