using MediatR;

namespace FinanceTracker.Application.UseCases.Category.Notifications;

public sealed record CategoryParentChangedNotification(
	Guid CategoryId,
	Guid UserId,
	Guid? OldParentId,
	Guid? NewParentId,
	DateTimeOffset OccurredAt
) : INotification;
