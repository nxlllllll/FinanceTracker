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

namespace FinanceTracker.Application.UseCases.Transaction.Commands.ExcludeTransaction;

public sealed class ExcludeTransactionHandler(
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	IDateProvider dateProvider,
	ILogger<ExcludeTransactionHandler> logger
) : IAuthorizedHandler<ExcludeTransactionCommand, Core.Domains.Transaction.Transaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		ExcludeTransactionCommand command,
		Core.Domains.Transaction.Transaction user,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = user.Exclude();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.ExcludeAsync(
				transactionId: command.TransactionId,
				userId: user.UserId,
				expectedVersion: user.RowVersion,
				ct: ct
			);

			if (user.Direction != DirectionType.Debit)
				return;

			await categoryTotalWriteRepository.SubtractAsync(
				userId: user.UserId,
				categoryId: user.CategoryId,
				currency: user.Amount.Currency,
				amount: user.Amount.Amount,
				occurredAt: user.OccurredAt,
				ct: ct
			);

			await budgetProgressWriteRepository.SubtractAsync(
				userId: user.UserId,
				categoryId: user.CategoryId,
				currencyCode: user.Amount.Currency,
				amount: user.Amount.Amount,
				occurredAt: user.OccurredAt,
				ct: ct
			);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to exclude transaction {user.Id}."),
		ct: ct);

		try
		{
			await publisher.Publish(notification: new TransactionExcludedNotification(
				TransactionId: user.Id,
				UserId: user.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransactionExcludedNotification for transaction {user.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: user.Id);
	}
}
