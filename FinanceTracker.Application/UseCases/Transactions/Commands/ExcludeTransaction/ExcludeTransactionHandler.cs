using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Operations;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transactions.Commands.ExcludeTransaction;

public sealed class ExcludeTransactionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork,
	ILogger<ExcludeTransactionHandler> logger,
	IOperationsWriteRepository operationsWriteRepository
) : IAuthorizedHandler<ExcludeTransactionCommand, Transaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ExcludeTransactionCommand command,
		Transaction transaction,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = transaction.Exclude();
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.ExcludeAsync(transactionId: command.TransactionId, ct: ct);
			await operationsWriteRepository.UpdateIsExcludedAsync(operationId: command.TransactionId, isExcluded: true, ct: ct);
			
			if (transaction.Direction != DirectionType.Debit)
				return;

			await categoryTotalWriteRepository.SubtractAsync(
				userId: transaction.UserId,
				categoryId: transaction.CategoryId,
				currency: transaction.Amount.Currency,
				amount: transaction.Amount.Amount,
				occurredAt: transaction.OccurredAt,
				ct: ct
			);

			await budgetProgressWriteRepository.SubtractAsync(
				userId: transaction.UserId,
				categoryId: transaction.CategoryId,
				currencyCode: transaction.Amount.Currency,
				amount: transaction.Amount.Amount,
				occurredAt: transaction.OccurredAt,
				ct: ct
			);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to exclude transaction {transaction.Id}."),
		ct: ct);
		
		return Result<Guid, DomainException>.Success(value: transaction.Id);
	}
}