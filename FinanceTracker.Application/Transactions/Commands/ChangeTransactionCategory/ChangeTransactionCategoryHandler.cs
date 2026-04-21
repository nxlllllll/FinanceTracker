using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionRepository transactionRepository,
	IPublisher publisher
) : IRequestHandler<ChangeTransactionCategoryCommand>
{
	public async Task Handle(
		ChangeTransactionCategoryCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = await transactionRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);
		
		transaction.ChangeCategory(categoryId: command.CategoryId);
		IReadOnlyList<IEvent> events = [..transaction.Events];
		
		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);

		await publisher.Publish(
			notification: new TransactionEventsNotification(TransactionId: command.TransactionId, Events: events), 
			cancellationToken: ct
		);
	}
}