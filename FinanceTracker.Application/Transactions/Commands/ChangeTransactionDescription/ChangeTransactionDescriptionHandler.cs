using FinanceTracker.Application.Transactions.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionRepository transactionRepository,
	IPublisher publisher
) : IRequestHandler<ChangeTransactionDescriptionCommand>
{
	public async Task Handle(
		ChangeTransactionDescriptionCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = await transactionRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);
		
		transaction.ChangeDescription(description: command.Description);
		IReadOnlyList<IEvent> events = [..transaction.Events];

		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);
		await publisher.Publish(
			notification: new TransactionEventsNotification(TransactionId: transaction.Id, Events: events),
			cancellationToken: ct
		);
	}
}