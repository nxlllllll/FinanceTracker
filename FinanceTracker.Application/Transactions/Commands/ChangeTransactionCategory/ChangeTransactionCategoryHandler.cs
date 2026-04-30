using FinanceTracker.Application.Behaviours.Authorization;
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
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IBudgetProgressWriteRepository budgetProgressWriteRepository
) : IAuthorizedHandler<ChangeTransactionCategoryCommand, TransactionDto>
{
	public async Task HandleAsync(
		ChangeTransactionCategoryCommand command,
		TransactionDto transaction,
		CancellationToken ct = default)
	{
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