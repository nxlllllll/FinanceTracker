namespace FinanceTracker.Core.Services.Notification;

public sealed record NotificationMessage(
	string Subject,
	string Body
);
