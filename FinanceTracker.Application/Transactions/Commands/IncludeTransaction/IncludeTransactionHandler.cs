using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.IncludeTransaction;

public sealed class IncludeTransactionHandler(
	ITransactionReadRepository transactionReadRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork
) : IRequestHandler<IncludeTransactionCommand>
{
	public async Task Handle(
		IncludeTransactionCommand command,
		CancellationToken ct = default)
	{
		TransactionDto transaction = await transactionReadRepository.GetByIdAsync(transactionId: command.TransactionId, ct: ct)
			?? throw new NotFoundException(message: "Transaction not found.", id: command.TransactionId);

		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await unitOfWork.CommitAsync(ct: ct);

			await transactionWriteRepository.IncludeAsync(transactionId: command.TransactionId, ct: ct);

			if (!transaction.IsExcluded)
				return;
		
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
		catch
		{
			await unitOfWork.RollbackAsync(ct: ct);
			throw;
		}
	}
}