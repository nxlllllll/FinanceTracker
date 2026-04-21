using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	ITransactionRepository transactionRepository,
	IPublisher publisher
) : IRequestHandler<CreateTransactionCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateTransactionCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = Transaction.Create(
			accountId: command.AccountId,
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: command.Amount,
			direction: command.Direction,
			exchangeRate: command.ExchangeRate,
			description: command.Description,
			occurredAt: command.OccurredAt
		);

		IReadOnlyList<IEvent> events = [..transaction.Events];
		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);

		await publisher.Publish(
			notification: new TransactionEventsNotification(
				TransactionId: transaction.Id,
				Events: events
			),
			cancellationToken: ct
		);
		
		return transaction.Id;
	}
}