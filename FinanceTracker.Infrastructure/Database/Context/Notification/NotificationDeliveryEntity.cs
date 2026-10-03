using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Infrastructure.Database.Context.Notification;

public sealed class NotificationDeliveryEntity
{
	public Guid EventId { get; init; }
	public NotificationType NotificationType { get; init; }
	public Guid UserId { get; init; }
	public DateTimeOffset DeliveredAt { get; init; }
}
