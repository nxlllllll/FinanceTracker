using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Services.Notification;

namespace FinanceTracker.Worker.Notification.Mapping;

public interface IUserNotificationMapper
{
	Type NotificationType { get; }

	Task<NotificationRequest> MapAsync(
		IUserNotification notification,
		CancellationToken ct = default
	);
}
