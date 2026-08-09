using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Worker.AccountProjection.Consumer;
using FinanceTracker.Worker.AccountProjection.Projection.Notifications;
using MediatR;
using ZLogger;

namespace FinanceTracker.Worker.AccountProjection.Projection;

/// <summary>
/// MediatR <see cref="INotificationHandler{TNotification}"/> that applies a batch
/// of account integration events to the read model in order.
/// Triggered by <see cref="AccountEventsConsumer"/> after deduplication.
/// </summary>
public sealed class AccountProjection(
	AccountEventApplier applier,
	ILogger<AccountProjection> logger
) : INotificationHandler<AccountEventsNotification>
{
	public async Task Handle(AccountEventsNotification notification, CancellationToken ct = default)
	{
		foreach (IIntegrationEvent @event in notification.Events)
		{
			await applier.ApplyAsync(@event: @event, ct: ct);
			string eventType = @event.GetType().Name;
			logger.ZLogDebug(message: $"Projected {eventType}.");
		}
	}
}
