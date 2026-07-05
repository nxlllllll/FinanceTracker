using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.IncludeTransaction;

public sealed class IncludeTransactionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<IncludeTransactionHandler> logger
) : IAuthorizedHandler<IncludeTransactionCommand, Core.Domains.Transaction.Transaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		IncludeTransactionCommand command,
		Core.Domains.Transaction.Transaction user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.Include();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.IncludeAsync(
				transactionId: command.TransactionId,
				userId: user.UserId,
				expectedVersion: user.RowVersion,
				ct: ct
			);

			if (user.Direction != DirectionType.Debit)
				return;

			await categoryTotalWriteRepository.AddAsync(
				userId: user.UserId,
				categoryId: user.CategoryId,
				currency: user.Amount.Currency,
				amount: user.Amount.Amount,
				occurredAt: user.OccurredAt,
				ct: ct
			);

			await budgetProgressWriteRepository.AddAsync(
				userId: user.UserId,
				categoryId: user.CategoryId,
				currencyCode: user.Amount.Currency,
				amount: user.Amount.Amount,
				occurredAt: user.OccurredAt,
				ct: ct
			);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to include transaction {user.Id}."),
		ct: ct);

		try
		{
			await publisher.Publish(notification: new TransactionIncludedNotification(
				TransactionId: user.Id,
				UserId: user.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransactionIncludedNotification for transaction {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
