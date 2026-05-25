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

namespace FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	ILogger<ChangeTransactionCategoryHandler> logger,
	IOperationsWriteRepository operationsWriteRepository
) : IAuthorizedHandler<ChangeTransactionCategoryCommand, Transaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeTransactionCategoryCommand command,
		Transaction transaction,
		CancellationToken ct = default)
	{
		if (transaction.CategoryId == command.CategoryId)
			return Result<Guid, DomainException>.Success(value: transaction.Id);

		Guid oldCategoryId = transaction.CategoryId;
		Result<Unit, DomainException> result = transaction.ChangeCategory(categoryId: command.CategoryId);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		try
		{
			await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
			{
				await transactionWriteRepository.ChangeCategoryAsync(
					transactionId: command.TransactionId,
					categoryId: command.CategoryId,
					ct: ct
				);

				await operationsWriteRepository.UpdateCategoryAsync(
					operationId: command.TransactionId,
					categoryId: command.CategoryId,
					ct: ct
				);

				if (transaction is not { IsExcluded: false, Direction: DirectionType.Debit })
					return;

				await categoryTotalWriteRepository.ChangeCategoryAsync(
					userId: transaction.UserId,
					oldCategoryId: oldCategoryId,
					newCategoryId: command.CategoryId,
					currency: transaction.Amount.Currency,
					amount: transaction.Amount.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);

				await budgetProgressWriteRepository.ChangeCategoryAsync(
					userId: transaction.UserId,
					oldCategoryId: oldCategoryId,
					newCategoryId: command.CategoryId,
					currencyCode: transaction.Amount.Currency,
					amount: transaction.Amount.Amount,
					occurredAt: transaction.OccurredAt,
					ct: ct
				);
			}, ct: ct);
		}
		catch (DomainException exception)
		{
			logger.ZLogError(exception: exception, message: $"Failed to change category for transaction {transaction.Id}.");
			return Result<Guid, DomainException>.Failure(error: exception);
		}
		
		return Result<Guid, DomainException>.Success(value: transaction.Id);
	}
}
