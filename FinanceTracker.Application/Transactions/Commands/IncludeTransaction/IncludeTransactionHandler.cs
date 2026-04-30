using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
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
) : IAuthorizedHandler<IncludeTransactionCommand, Transaction>
{
	public async Task HandleAsync(
		IncludeTransactionCommand command,
		Transaction transaction,
		CancellationToken ct = default)
	{
		transaction.Include();
		
		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await transactionWriteRepository.IncludeAsync(transactionId: command.TransactionId, ct: ct);

			if (transaction.Direction == DirectionType.Debit)
			{
				await categoryTotalWriteRepository.AddAsync(
					userId: transaction.UserId,
					categoryId: transaction.CategoryId,
					amount: transaction.Amount.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);
			
				await budgetProgressWriteRepository.AddAsync(
					userId: transaction.UserId,
					categoryId: transaction.CategoryId,
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