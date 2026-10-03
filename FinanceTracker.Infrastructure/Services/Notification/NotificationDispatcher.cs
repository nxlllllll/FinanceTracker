using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Repositories.Notification;
using FinanceTracker.Core.Repositories.User;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Configurations.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Infrastructure.Services.Notification;

public sealed class NotificationDispatcher(
	IUserAuthRepository userAuthRepository,
	INotificationDeliveryRepository notificationDeliveryRepository,
	IEnumerable<INotificationSender> notificationSenders,
	IOptionsMonitor<NotificationOptions> options,
	IDateProvider dateProvider,
	ILogger<NotificationDispatcher> logger
) : INotificationDispatcher
{
	public async Task SendAsync(
		NotificationRequest request,
		CancellationToken ct = default)
	{
		NotificationOptions currentOptions = options.CurrentValue;

		User? user = await userAuthRepository.GetByIdAsync(userId: request.UserId, ct: ct);
		if (user?.NotificationType is not { } type)
		{
			logger.ZLogDebug(message: $"Notification {request.EventId} skipped: user {request.UserId} is gone or has notifications off.");
			return;
		}

		if (await notificationDeliveryRepository.IsDeliveredAsync(eventId: request.EventId, type: type, ct: ct))
		{
			logger.ZLogWarning(message: $"Notification {request.EventId} was already delivered by {type}.");
			return;
		}

		INotificationSender sender = notificationSenders.FirstOrDefault(predicate: s => s.Type == type)
			?? throw new InvalidOperationException(message: $"No notification sender is registered for {type}.");

		if (!currentOptions.Templates.TryGetValue(key: request.Template, value: out NotificationTemplate? template))
			throw new InvalidOperationException(message: $"Notification template '{request.Template}' is not configured.");

		await sender.SendAsync(
			recipient: new NotificationRecipient(UserId: user.Id, Email: user.Email),
			message: NotificationTemplateRenderer.Render(template: template, values: request.Values),
			ct: ct
		);

		await notificationDeliveryRepository.RecordAsync(
			eventId: request.EventId,
			type: type,
			userId: user.Id,
			deliveredAt: dateProvider.UtcNow,
			ct: ct
		);
	}
}
