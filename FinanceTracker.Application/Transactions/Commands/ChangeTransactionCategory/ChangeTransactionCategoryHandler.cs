using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository
) : IRequestHandler<ChangeTransactionCategoryCommand>
{
	public async Task Handle(
		ChangeTransactionCategoryCommand command,
		CancellationToken ct = default)
	{
		TransactionDto transaction = await transactionReadRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);
		
		await transactionWriteRepository.ChangeCategoryAsync(
			transactionId: command.TransactionId,
			categoryId: command.CategoryId,
			ct: ct
		);
		
		if (transaction.IsExcluded) 
			return;
		
		await categoryTotalWriteRepository.ChangeCategoryAsync(
			userId: transaction.UserId,
			oldCategoryId: transaction.CategoryId,
			newCategoryId: command.CategoryId,
			amount: transaction.Amount,
			occurredAt: transaction.OccurredAt,
			ct: ct
		);
	}
}