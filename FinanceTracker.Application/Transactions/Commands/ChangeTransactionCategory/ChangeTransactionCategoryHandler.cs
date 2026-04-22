using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository
) : IRequestHandler<ChangeTransactionCategoryCommand>
{
	public async Task Handle(
		ChangeTransactionCategoryCommand command,
		CancellationToken ct = default)
	{
		bool exists = await transactionReadRepository.ExistsAsync(
			transactionId: command.TransactionId, ct: ct
		);

		if (!exists)
			throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);

		await transactionWriteRepository.ChangeCategoryAsync(
			transactionId: command.TransactionId,
			categoryId: command.CategoryId,
			ct: ct
		);
	}
}