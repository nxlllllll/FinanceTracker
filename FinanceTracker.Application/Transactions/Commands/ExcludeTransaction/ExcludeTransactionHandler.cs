using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.Transactions.Commands.ExcludeTransaction;

public sealed class ExcludeTransactionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<ExcludeTransactionCommand, TransactionDto>
{
	public async Task HandleAsync(
		ExcludeTransactionCommand command,
		TransactionDto transaction,
		CancellationToken ct = default)
	{
		if (transaction.IsExcluded)
			return;
		
		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await transactionWriteRepository.ExcludeAsync(transactionId: command.TransactionId, ct: ct);

			if (transaction.Direction == DirectionType.Debit)
			{
				await categoryTotalWriteRepository.SubtractAsync(
					userId: transaction.UserId,
					categoryId: transaction.CategoryId,
					amount: transaction.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);

				await budgetProgressWriteRepository.SubtractAsync(
					userId: transaction.UserId,
					categoryId: transaction.CategoryId,
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