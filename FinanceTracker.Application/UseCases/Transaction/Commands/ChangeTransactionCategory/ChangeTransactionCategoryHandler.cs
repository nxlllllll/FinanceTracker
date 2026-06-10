using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryReadRepository categoryReadRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider
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
		if (category is null)
			return Result<Guid, DomainException>.Failure(error: new NotFoundException(message: "Category not found.", id: command.CategoryId));

		DomainException? validationResult = CategoryDirectionValidator.Validate(category: category, direction: transaction.Direction);
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
		
		await publisher.Publish(notification: new TransactionCategoryChangedNotification(
			TransactionId: transaction.Id,
			UserId: transaction.UserId,
			OldCategoryId: oldCategoryId,
			NewCategoryId: command.CategoryId,
			OccurredAt: dateProvider.UtcNow
		), cancellationToken: ct);

		return Result<Guid, DomainException>.Success(value: transaction.Id);
	}
}
