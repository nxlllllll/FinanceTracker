using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;

public sealed class ExcludeTransactionHandler(
	ITransactionRepository transactionRepository
) : IRequestHandler<ExcludeTransactionCommand>
{
	public async Task Handle(
		ExcludeTransactionCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = await transactionRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);

		transaction.Exclude();

		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);
	}
}