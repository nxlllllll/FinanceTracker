using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Worker.AccountProjection.Projection.Notifications;
using MediatR;
using ZLogger;

namespace FinanceTracker.Worker.AccountProjection.Projection;

public sealed class AccountProjection(
	AccountEventApplier applier,
	ILogger<AccountProjection> logger
) : INotificationHandler<AccountEventsNotification>
{
	public async Task Handle(AccountEventsNotification notification, CancellationToken ct = default)
	{
		foreach (IAccountIntegrationEvent @event in notification.Events)
		{
			await applier.ApplyAsync(@event: @event, ct: ct);
			logger.ZLogDebug(message: $"Projected {@event.GetType().Name}.");
		}
	}
}