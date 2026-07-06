using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Application.UseCases.Transaction.Services;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	ITransactionCreationService transactionCreationService,
	ICategoryReadRepository categoryReadRepository,
	IPublisher publisher,
	ILogger<CreateTransactionHandler> logger
) : IAuthorizedHandler<CreateTransactionCommand, Core.Domains.Account.Account, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		CreateTransactionCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		CategoryReadModel? category = await categoryReadRepository.GetByIdAsync(categoryId: command.CategoryId, userId: command.UserId, ct: ct);
		if (category is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: "Category not found.", id: command.CategoryId));

		DomainException? validationResult = CategoryDirectionValidator.Validate(category: category, direction: command.Direction);
		if (validationResult is not null)
			return Result<Guid, AppException>.Failure(error: validationResult);

		if (category.IsArchived)
			return Result<Guid, AppException>.Failure(error: new ArchivedOperationException(message: "Cannot create a transaction for an archived category."));

		Result<Core.Domains.Transaction.Transaction, DomainException> result = await transactionCreationService.CreateAsync(command: command, account: account, ct: ct);
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		Core.Domains.Transaction.Transaction transaction = result.Value!;

		try
		{
			await publisher.Publish(notification: new TransactionCreatedNotification(
				TransactionId: transaction.Id,
				AccountId: transaction.AccountId,
				UserId: transaction.UserId,
				CategoryId: transaction.CategoryId,
				Amount: transaction.Amount,
				Direction: transaction.Direction,
				ExchangeRate: transaction.ExchangeRate,
				IsRatePending: transaction.IsRatePending,
				Description: transaction.Description,
				OccurredAt: transaction.OccurredAt
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransactionCreatedNotification for transaction {transaction.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: transaction.Id);
	}
}
