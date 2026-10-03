namespace FinanceTracker.Core.Services.Notification;

public interface INotificationDispatcher
{
	Task SendAsync(
		NotificationRequest request,
		CancellationToken ct = default
	);
}
