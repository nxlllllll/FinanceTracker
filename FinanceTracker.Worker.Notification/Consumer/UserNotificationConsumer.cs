using System.Runtime.Serialization;
using System.Text.Json;
using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Contracts.Messages;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Services.Notification;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Worker.Notification.Mapping;
using FinanceTracker.Worker.Shared.RabbitMQ.Handler;
using ZLogger;

namespace FinanceTracker.Worker.Notification.Consumer;

[RoutingKey(routingKey: IUserNotification.RoutingKey)]
public sealed class UserNotificationConsumer(
	IEnumerable<IUserNotificationMapper> mappers,
	INotificationDispatcher notificationDispatcher,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	ILogger<UserNotificationConsumer> logger
) : IMessageHandler<AggregateEventsMessage>
{
	public async Task HandleAsync(AggregateEventsMessage message, CancellationToken ct = default)
	{
		foreach (IUserNotification notification in ExtractNotifications(message: message))
		{
			IUserNotificationMapper mapper = mappers.FirstOrDefault(predicate: m => m.NotificationType == notification.GetType())
				?? throw new InvalidOperationException(message: $"No notification mapper is registered for {notification.GetType().Name}.");

			NotificationRequest request = await mapper.MapAsync(notification: notification, ct: ct);

			await notificationDispatcher.SendAsync(request: request, ct: ct);
		}
	}

	private List<IUserNotification> ExtractNotifications(AggregateEventsMessage message)
	{
		List<IUserNotification> notifications = [];

		foreach (EventEnvelope envelope in message.Events)
		{
			Type type;
			try
			{
				type = integrationEventTypeResolver.ResolveType(eventType: envelope.EventType);
			}
			catch (InvalidOperationException)
			{
				logger.ZLogDebug(message: $"Skipping unrecognised event type '{envelope.EventType}'.");
				continue;
			}

			if (!typeof(IUserNotification).IsAssignableFrom(c: type))
				continue;

			notifications.Add(item: (IUserNotification?)JsonSerializer.Deserialize(
				json: envelope.EventPayload,
				returnType: type,
				options: FinanceTrackerJsonOptions.Payload
			) ?? throw new SerializationException(message: $"Payload of '{envelope.EventType}' deserialized to null."));
		}

		return notifications;
	}
}
