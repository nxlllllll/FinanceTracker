using System.Text.Json;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.Notification;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Notification;

public sealed class NotificationDeliveryRepository(
	FinanceTrackerContext context
) : INotificationDeliveryRepository
{
	public Task<bool> IsDeliveredAsync(
		Guid eventId,
		NotificationType type,
		CancellationToken ct = default)
	{
		return context.NotificationDeliveries.AsNoTracking().AnyAsync(
			predicate: d => d.EventId == eventId && d.NotificationType == type,
			cancellationToken: ct
		);
	}

	public async Task RecordAsync(
		Guid eventId,
		NotificationType type,
		Guid userId,
		DateTimeOffset deliveredAt,
		CancellationToken ct = default)
	{
		string typeCode = JsonNamingPolicy.SnakeCaseLower.ConvertName(name: type.ToString());

		await context.Database.ExecuteSqlAsync(sql: $"""
			INSERT INTO notification_deliveries (event_id, notification_type, user_id, delivered_at)
			VALUES ({eventId}, {typeCode}, {userId}, {deliveredAt})
			ON CONFLICT (event_id, notification_type) DO NOTHING
		""", cancellationToken: ct);
	}
}
