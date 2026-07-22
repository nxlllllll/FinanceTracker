using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Notifications;

public sealed record RoleAssignedToUserNotification(
	Guid UserId,
	Guid RoleId,
	Guid AssignedBy,
	DateTimeOffset OccurredAt
) : INotification;
