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
		Core.Domains.Transaction.Transaction accounts,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = accounts.Include();
		if (result.IsFailure)
			return Result<Guid, AppException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.IncludeAsync(
				transactionId: command.TransactionId,
				userId: accounts.UserId,
				expectedVersion: accounts.RowVersion,
				ct: ct
			);

			if (accounts.Direction != DirectionType.Debit)
				return;

			await categoryTotalWriteRepository.AddAsync(
				userId: accounts.UserId,
				categoryId: accounts.CategoryId,
				currency: accounts.Amount.Currency,
				amount: accounts.Amount.Amount,
				occurredAt: accounts.OccurredAt,
				ct: ct
			);

			await budgetProgressWriteRepository.AddAsync(
				userId: accounts.UserId,
				categoryId: accounts.CategoryId,
				currencyCode: accounts.Amount.Currency,
				amount: accounts.Amount.Amount,
				occurredAt: accounts.OccurredAt,
				ct: ct
			);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to include transaction {accounts.Id}."),
		ct: ct);

		try
		{
			await publisher.Publish(notification: new TransactionIncludedNotification(
				TransactionId: accounts.Id,
				UserId: accounts.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransactionIncludedNotification for transaction {accounts.Id} after successful commit.");
		}

		return Result<Guid, AppException>.Success(value: accounts.Id);
	}
}
