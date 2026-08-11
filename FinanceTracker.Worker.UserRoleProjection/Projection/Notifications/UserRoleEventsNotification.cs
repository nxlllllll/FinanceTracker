using FinanceTracker.Contracts.Events.Abstraction;
using INotification = MediatR.INotification;

namespace FinanceTracker.Worker.UserRoleProjection.Projection.Notifications;

public sealed record UserRoleEventsNotification(
	Guid UserId,
	IReadOnlyList<IIntegrationEvent> Events
) : INotification;
