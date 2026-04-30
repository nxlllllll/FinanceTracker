using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IBudgetProgressWriteRepository budgetProgressWriteRepository
) : IAuthorizedHandler<ChangeTransactionCategoryCommand, Transaction>
{
	public async Task HandleAsync(
		ChangeTransactionCategoryCommand command,
		Transaction transaction,
		CancellationToken ct = default)
	{
		if (transaction.CategoryId == command.CategoryId)
			return;

		transaction.ChangeCategory(categoryId: command.CategoryId);
		
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
					amount: transaction.Amount.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);

				await budgetProgressWriteRepository.ChangeCategoryAsync(
					userId: transaction.UserId,
					oldCategoryId: transaction.CategoryId,
					newCategoryId: command.CategoryId,
					currencyCode: transaction.Amount.Currency,
					amount: transaction.Amount.Amount,
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