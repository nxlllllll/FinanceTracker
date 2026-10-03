using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Services.Notification;

namespace FinanceTracker.Worker.Notification.Mapping;

public abstract class UserNotificationMapper<TNotification> 
	: IUserNotificationMapper where TNotification : IUserNotification
{
	public Type NotificationType => typeof(TNotification);

	public Task<NotificationRequest> MapAsync(
		IUserNotification notification,
		CancellationToken ct = default
	) => MapAsync(notification: (TNotification)notification, ct: ct);

	protected abstract Task<NotificationRequest> MapAsync(
		TNotification notification,
		CancellationToken ct
	);
}
