using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionRepository transactionRepository
) : IRequestHandler<ChangeTransactionCategoryCommand>
{
	public async Task Handle(
		ChangeTransactionCategoryCommand command,
		CancellationToken ct = default)
	{
		Transaction transaction = await transactionRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);
		
		transaction.ChangeCategory(categoryId: command.CategoryId);

		await transactionRepository.SaveAsync(transaction: transaction, ct: ct);
	}
}