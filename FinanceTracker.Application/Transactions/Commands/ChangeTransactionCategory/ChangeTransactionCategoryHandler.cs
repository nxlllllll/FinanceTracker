using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IBudgetProgressWriteRepository budgetProgressWriteRepository
) : IRequestHandler<ChangeTransactionCategoryCommand>
{
	public async Task Handle(
		ChangeTransactionCategoryCommand command,
		CancellationToken ct = default)
	{
		TransactionDto transaction = await transactionReadRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);
		
		if (transaction.UserId != command.UserId)
			throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);
		
		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await transactionWriteRepository.ChangeCategoryAsync(
				transactionId: command.TransactionId,
				categoryId: command.CategoryId,
				ct: ct
			);

			if (transaction is { IsExcluded: false, Direction: DirectionType.Debit })
			{
				await categoryTotalWriteRepository.ChangeCategoryAsync(
					userId: transaction.UserId,
					oldCategoryId: transaction.CategoryId,
					newCategoryId: command.CategoryId,
					amount: transaction.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);

				await budgetProgressWriteRepository.ChangeCategoryAsync(
					userId: transaction.UserId,
					oldCategoryId: transaction.CategoryId,
					newCategoryId: command.CategoryId,
					currencyCode: transaction.Currency,
					amount: transaction.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);
			}
			
			await unitOfWork.CommitAsync(ct: ct);
		}
		catch
		{
			await unitOfWork.RollbackAsync(ct: ct);
			throw;
		}
	}
}