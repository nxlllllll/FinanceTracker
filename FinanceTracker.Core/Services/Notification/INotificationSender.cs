using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Core.Services.Notification;

public interface INotificationSender
{
	/// <summary>
	/// The channel this sender delivers through; the worker picks a sender by the user's choice.
	/// </summary>
	NotificationType Type { get; }

	Task SendAsync(
		NotificationRecipient recipient,
		NotificationMessage message,
		CancellationToken ct = default
	);
}
