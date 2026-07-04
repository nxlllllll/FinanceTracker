using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;

public sealed class ChangeTransactionCategoryHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryReadRepository categoryReadRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IUnitOfWork unitOfWork,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ChangeTransactionCategoryHandler> logger
) : IAuthorizedHandler<ChangeTransactionCategoryCommand, Core.Domains.Transaction.Transaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ChangeTransactionCategoryCommand command,
		Core.Domains.Transaction.Transaction accounts,
		CancellationToken ct = default)
	{
		if (accounts.CategoryId == command.CategoryId)
			return Result<Guid, AppException>.Success(value: accounts.Id);

		CategoryReadModel? category = await categoryReadRepository.GetByIdAsync(categoryId: command.CategoryId, userId: command.UserId, ct: ct);
		if (category is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Category not found.", id: command.CategoryId));

		DomainException? validationResult = CategoryDirectionValidator.Validate(category: category, direction: accounts.Direction);
		if (validationResult is not null)
			return Result<Guid, AppException>.Failure(error: validationResult);

		Guid oldCategoryId = accounts.CategoryId;
		Result<Unit, DomainException> result = accounts.ChangeCategory(categoryId: command.CategoryId);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.ChangeCategoryAsync(
				transactionId: command.TransactionId,
				userId: accounts.UserId,
				categoryId: command.CategoryId,
				expectedVersion: accounts.RowVersion,
				ct: ct
			);

			if (accounts is not { IsExcluded: false, Direction: DirectionType.Debit })
				return;

			await categoryTotalWriteRepository.ChangeCategoryAsync(
				userId: accounts.UserId,
				oldCategoryId: oldCategoryId,
				newCategoryId: command.CategoryId,
				currency: accounts.Amount.Currency,
				amount: accounts.Amount.Amount,
				occurredAt: accounts.OccurredAt,
				ct: ct
			);

			await budgetProgressWriteRepository.ChangeCategoryAsync(
				userId: accounts.UserId,
				oldCategoryId: oldCategoryId,
				newCategoryId: command.CategoryId,
				currencyCode: accounts.Amount.Currency,
				amount: accounts.Amount.Amount,
				occurredAt: accounts.OccurredAt,
				ct: ct
			);
		}, ct: ct);

		try
		{
			await publisher.Publish(notification: new TransactionCategoryChangedNotification(
				TransactionId: accounts.Id,
				UserId: accounts.UserId,
				OldCategoryId: oldCategoryId,
				NewCategoryId: command.CategoryId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransactionCategoryChangedNotification for transaction {accounts.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: accounts.Id);
	}
}
