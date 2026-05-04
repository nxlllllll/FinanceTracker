using FinanceTracker.Application.Abstractions;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.ConfigurationExceptions;
using MediatR;

namespace FinanceTracker.Application.Dispatching;

public sealed class MediatRNotificationDispatcher(
	IPublisher publisher
) : INotificationDispatcher
{
	public Task DispatchAsync(IAppNotification notification, CancellationToken ct = default)
	{
		if (notification.Data is not IMediatRConvertible convertible)
			throw new UnknownAggregateTypeException(message: $"Notification data of type is not MediatR convertible.", aggregateType: notification.Data.GetType().Name);
		
		return publisher.Publish(convertible.ToMediatRNotification(), ct);
	}
}