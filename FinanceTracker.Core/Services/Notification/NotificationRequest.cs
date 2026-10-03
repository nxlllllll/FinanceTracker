namespace FinanceTracker.Core.Services.Notification;

public sealed record NotificationRequest(
	Guid EventId,
	Guid UserId,
	string Template,
	IReadOnlyDictionary<string, string> Values
);
