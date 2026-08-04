using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Worker.UserRoleProjection.Projection.Notifications;
using MediatR;
using ZLogger;

namespace FinanceTracker.Worker.UserRoleProjection.Projection;

public sealed class UserRoleProjection(
	UserRoleEventApplier applier,
	ILogger<UserRoleProjection> logger
) : INotificationHandler<UserRoleEventsNotification>
{
	public async Task Handle(UserRoleEventsNotification notification, CancellationToken ct = default)
	{
		foreach (IIntegrationEvent @event in notification.Events)
		{
			await applier.ApplyAsync(@event: @event, ct: ct);
			logger.ZLogDebug(message: $"Projected {@event.GetType().Name}.");
		}
	}
}
