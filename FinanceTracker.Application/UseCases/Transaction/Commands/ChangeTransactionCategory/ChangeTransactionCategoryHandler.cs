using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryReadRepository categoryReadRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IBudgetProgressWriteRepository budgetProgressWriteRepository
) : IAuthorizedHandler<ChangeTransactionCategoryCommand, Core.Domains.Transaction.Transaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ChangeTransactionCategoryCommand command,
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		if (transaction.CategoryId == command.CategoryId)
			return Result<Guid, DomainException>.Success(value: transaction.Id);
		
		CategoryReadModel? category = await categoryReadRepository.GetByIdAsync(categoryId: command.CategoryId, userId: command.UserId, ct: ct);
		DomainException? validationResult = CategoryDirectionValidator.Validate(category: category, direction: transaction.Direction, categoryId: command.CategoryId);
		if (validationResult is not null)
			return Result<Guid, DomainException>.Failure(error: validationResult);
			
		Guid oldCategoryId = transaction.CategoryId;
		Result<Unit, DomainException> result = transaction.ChangeCategory(categoryId: command.CategoryId);
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);
		
		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.ChangeCategoryAsync(
				transactionId: command.TransactionId,
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

		return Result<Guid, DomainException>.Success(value: transaction.Id);
	}
}
