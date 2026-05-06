using FinanceTracker.Application.Abstractions;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.Dispatching;

public sealed class MediatRNotificationDispatcher(
	IPublisher publisher,
	ILogger<MediatRNotificationDispatcher> logger
) : INotificationDispatcher
{
	public Task DispatchAsync(IAppNotification notification, CancellationToken ct = default)
	{
		if (notification.Data is not IMediatRConvertible convertible)
			throw new UnknownAggregateTypeException(message: "Notification data of type is not MediatR convertible.", aggregateType: notification.Data.GetType().Name);
		
		logger.ZLogDebug(message: $"Dispatching notification {notification.Data.GetType().Name}.");
		return publisher.Publish(notification: convertible.ToMediatRNotification(), cancellationToken: ct);
	}
}