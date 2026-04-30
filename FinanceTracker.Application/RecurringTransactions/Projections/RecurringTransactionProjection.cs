using FinanceTracker.Application.RecurringTransactions.Notifications;
using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using MediatR;

namespace FinanceTracker.Application.RecurringTransactions.Projections;

public sealed class RecurringTransactionProjection(
	IMediator mediator
) : INotificationHandler<TransactionDataNotification>
{
	public async Task Handle(
		TransactionDataNotification notification,
		CancellationToken ct = default)
	{
		await mediator.Send(request: new CreateTransactionCommand(
			AccountId: notification.AccountId,
			UserId: notification.UserId,
			CategoryId: notification.CategoryId,
			Amount: notification.Amount,
			Currency: notification.Currency,
			Direction: notification.Direction,
			Description: notification.Description,
			OccurredAt: notification.OccurredAt
		), cancellationToken: ct);
	}
}