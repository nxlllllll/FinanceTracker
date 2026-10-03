using FinanceTracker.Core.Domains.User;

namespace FinanceTracker.Core.Repositories.Notification;

public interface INotificationDeliveryRepository
{
	Task<bool> IsDeliveredAsync(
		Guid eventId,
		NotificationType type,
		CancellationToken ct = default
	);

	Task RecordAsync(
		Guid eventId,
		NotificationType type,
		Guid userId,
		DateTimeOffset deliveredAt,
		CancellationToken ct = default
	);
}
