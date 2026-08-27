using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Transaction.Notifications;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transaction.Commands.CancelTransaction;

public sealed class CancelTransactionHandler(
	IAccountRepository accountRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider,
	IOptionsMonitor<CancellationOptions> cancellation,
	ILogger<CancelTransactionHandler> logger
) : IAuthorizedHandler<CancelTransactionCommand, Core.Domains.Transaction.Transaction, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		CancelTransactionCommand command,
		Core.Domains.Transaction.Transaction transaction,
		CancellationToken ct = default)
	{
		bool wasExcluded = transaction.IsExcluded;

		Result<Unit, DomainException> cancelled = transaction.Cancel(
			cancelledAt: dateProvider.UtcNow,
			maxAge: TimeSpan.FromDays(value: cancellation.CurrentValue.MaxAgeDays)
		);

		if (cancelled.IsFailure)
			return Result<Guid, AppException>.Failure(error: cancelled.Error!);

		Core.Domains.Account.Account? account = await accountRepository.GetByIdAsync(accountId: transaction.AccountId, ct: ct);
		if (account is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: $"Account {transaction.AccountId} was not found.", id: transaction.AccountId));

		Result<Unit, DomainException> reverted = account.RevertTransaction(
			occurredAt: dateProvider.UtcNow,
			transactionId: transaction.Id,
			categoryId: transaction.CategoryId,
			amount: transaction.Amount.Amount,
			exchangeRate: transaction.ExchangeRate,
			direction: transaction.Direction,
			description: transaction.Description
		);

		if (reverted.IsFailure)
			return Result<Guid, AppException>.Failure(error: reverted.Error!);

		Guid reversalId = Guid.CreateVersion7();

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await accountRepository.SaveAsync(account: account, ct: ct);

			await transactionWriteRepository.CancelAsync(
				transaction: transaction,
				reversalId: reversalId,
				ct: ct
			);

			if (wasExcluded)
				return;

			await categoryTotalWriteRepository.SubtractAsync(
				userId: transaction.UserId,
				categoryId: transaction.CategoryId,
				currency: transaction.Amount.Currency,
				amount: transaction.Amount.Amount,
				occurredAt: transaction.OccurredAt,
				ct: ct
			);

			if (transaction.Direction != DirectionType.Debit)
				return;

			await budgetProgressWriteRepository.SubtractAsync(
				userId: transaction.UserId,
				categoryId: transaction.CategoryId,
				currencyCode: transaction.Amount.Currency,
				amount: transaction.Amount.Amount,
				occurredAt: transaction.OccurredAt,
				ct: ct
			);
		},
		onError: exception =>
		{
			logger.ZLogError(exception: exception, message: $"Failed to cancel transaction {transaction.Id}.");
			return Task.CompletedTask;
		},
		ct: ct);

		postCommitNotifications.Stage(notification: new TransactionCancelledNotification(
			TransactionId: transaction.Id,
			UserId: transaction.UserId,
			AccountId: transaction.AccountId,
			ReversalId: reversalId,
			Amount: transaction.Amount.Amount,
			Direction: transaction.Direction,
			WasExcluded: wasExcluded,
			OccurredAt: dateProvider.UtcNow
		));

		return Result<Guid, AppException>.Success(value: transaction.Id);
	}
}
