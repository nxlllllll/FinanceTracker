using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using MediatR;

namespace FinanceTracker.Application.Dispatching;

public sealed class MediatRNotificationDispatcher(
	IPublisher publisher
) : INotificationDispatcher
{
	public Task DispatchAsync(
		AggregateNotification notification,
		CancellationToken ct = default)
	{
		INotification mediatRNotification = notification.AggregateType switch
		{
			nameof(Account) => new AccountEventsNotification(
				AccountId: notification.AggregateId,
				Events: notification.Events
			),
			_ => throw new UnknownAggregateTypeException(message: "Unknown aggregate type.", aggregateType: notification.AggregateType)
		};

		return publisher.Publish(notification: mediatRNotification, cancellationToken: ct);
	}
}