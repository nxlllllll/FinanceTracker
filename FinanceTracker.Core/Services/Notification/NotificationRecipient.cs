using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Services.Notification;

public sealed record NotificationRecipient(
	Guid UserId,
	Email Email
);
