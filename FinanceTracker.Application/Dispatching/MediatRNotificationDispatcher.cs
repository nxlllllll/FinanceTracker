using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.RecurringTransactions.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Account.Notification;
using FinanceTracker.Core.Domains.RecurringTransaction;
using FinanceTracker.Core.Exceptions;
using MediatR;

namespace FinanceTracker.Application.Dispatching;

public sealed class MediatRNotificationDispatcher(
	IPublisher publisher
) : INotificationDispatcher
{
	public Task DispatchAsync(
		Notification notification,
		CancellationToken ct = default)
	{
		INotification mediatRNotification = notification.Data switch
		{
			AccountNotification n => new AccountEventsNotification(
				AccountId: n.AccountId,
				Events: n.Events
			),
			RecurringTransactionNotification n => new TransactionDataNotification(
				AccountId: n.AccountId,
				UserId: n.UserId,
				CategoryId: n.CategoryId,
				Amount: n.Amount,
				Currency: n.Currency,
				Direction: n.Direction,
				Description: n.Description,
				OccurredAt: n.OccurredAt
			),
			_ => throw new UnknownAggregateTypeException(message: "Unknown aggregate type.", aggregateType: notification.Data.GetType().Name)
		};

		return publisher.Publish(notification: mediatRNotification, cancellationToken: ct);
	}
}