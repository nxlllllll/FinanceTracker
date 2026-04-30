using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;

namespace FinanceTracker.Application.Transactions.Commands.IncludeTransaction;

public sealed class IncludeTransactionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<IncludeTransactionCommand, TransactionDto>
{
	public async Task HandleAsync(
		IncludeTransactionCommand command,
		TransactionDto transaction,
		CancellationToken ct = default)
	{
		if (!transaction.IsExcluded)
			return;
		
		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await transactionWriteRepository.IncludeAsync(transactionId: command.TransactionId, ct: ct);

			if (transaction.Direction == DirectionType.Debit)
			{
				await categoryTotalWriteRepository.AddAsync(
					userId: transaction.UserId,
					categoryId: transaction.CategoryId,
					amount: transaction.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);
			
				await budgetProgressWriteRepository.AddAsync(
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