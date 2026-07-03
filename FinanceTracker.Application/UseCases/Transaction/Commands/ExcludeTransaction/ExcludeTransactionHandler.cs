using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Domains.Account;
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
) : IAuthorizedHandler<ExcludeTransactionCommand, Core.Domains.Transaction.Transaction, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		ExcludeTransactionCommand command,
		Core.Domains.Transaction.Transaction accounts,
		CancellationToken ct = default)
	{
		Result<Unit, DomainException> result = accounts.Exclude();
		if (result.IsFailure)
			return Result<Guid, DomainException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.ExcludeAsync(
				transactionId: command.TransactionId,
				userId: accounts.UserId,
				expectedVersion: accounts.RowVersion,
				ct: ct
			);

			if (accounts.Direction != DirectionType.Debit)
				return;

			await categoryTotalWriteRepository.SubtractAsync(
				userId: accounts.UserId,
				categoryId: accounts.CategoryId,
				currency: accounts.Amount.Currency,
				amount: accounts.Amount.Amount,
				occurredAt: accounts.OccurredAt,
				ct: ct
			);

			await budgetProgressWriteRepository.SubtractAsync(
				userId: accounts.UserId,
				categoryId: accounts.CategoryId,
				currencyCode: accounts.Amount.Currency,
				amount: accounts.Amount.Amount,
				occurredAt: accounts.OccurredAt,
				ct: ct
			);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to exclude transaction {accounts.Id}."),
		ct: ct);

		try
		{
			await publisher.Publish(notification: new TransactionExcludedNotification(
				TransactionId: accounts.Id,
				UserId: accounts.UserId,
				OccurredAt: dateProvider.UtcNow
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransactionExcludedNotification for transaction {accounts.Id} after successful commit.");
		}

		return Result<Guid, DomainException>.Success(value: accounts.Id);
	}
}
