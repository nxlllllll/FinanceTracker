using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Notifications;

public sealed record RoleRemovedFromUserNotification(
	Guid UserId,
	Guid RoleId,
	Guid RemovedBy,
	DateTimeOffset OccurredAt
) : INotification;
