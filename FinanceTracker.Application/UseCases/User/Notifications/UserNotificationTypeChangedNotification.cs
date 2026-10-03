using FinanceTracker.Core.Domains.User;
using MediatR;

namespace FinanceTracker.Application.UseCases.User.Notifications;

public sealed record UserNotificationTypeChangedNotification(
	Guid UserId,
	NotificationType? OldNotificationType,
	NotificationType? NewNotificationType,
	DateTimeOffset OccurredAt
) : INotification;
