using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Worker.PermissionProjection.Projection.Notifications;
using MediatR;
using ZLogger;

namespace FinanceTracker.Worker.PermissionProjection.Projection;

/// <summary>
/// MediatR <see cref="INotificationHandler{TNotification}"/> that applies a batch
/// of permission integration events to the read model in order.
/// </summary>
public sealed class PermissionProjection(
	PermissionEventApplier applier,
	ILogger<PermissionProjection> logger
) : INotificationHandler<PermissionEventsNotification>
{
	public async Task Handle(PermissionEventsNotification notification, CancellationToken ct = default)
	{
		foreach (IIntegrationEvent @event in notification.Events)
		{
			await applier.ApplyAsync(@event: @event, ct: ct);
			logger.ZLogDebug(message: $"Projected {@event.GetType().Name}.");
		}
	}
}
