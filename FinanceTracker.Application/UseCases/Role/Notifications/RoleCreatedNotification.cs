using MediatR;

namespace FinanceTracker.Application.UseCases.Role.Notifications;

public sealed record RoleCreatedNotification(
	Guid RoleId,
	string DisplayName,
	IReadOnlySet<string> Permissions,
	DateTimeOffset OccurredAt
) : INotification;
