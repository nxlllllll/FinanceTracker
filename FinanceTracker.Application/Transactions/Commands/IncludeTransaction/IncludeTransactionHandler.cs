using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.IncludeTransaction;

public sealed class IncludeTransactionHandler(
	ITransactionRepository transactionRepository
) : IRequestHandler<IncludeTransactionCommand>
{
	public async Task Handle(
		IncludeTransactionCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = await transactionRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);

		transaction.Include();

		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);
	}
}