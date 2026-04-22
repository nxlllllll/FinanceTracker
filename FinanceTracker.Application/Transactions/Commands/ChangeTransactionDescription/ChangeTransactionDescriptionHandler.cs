using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionDescription;

public sealed class ChangeTransactionDescriptionHandler(
	ITransactionRepository transactionRepository
) : IRequestHandler<ChangeTransactionDescriptionCommand>
{
	public async Task Handle(
		ChangeTransactionDescriptionCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = await transactionRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);
		
		transaction.ChangeDescription(description: command.Description);

		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);
	}
}