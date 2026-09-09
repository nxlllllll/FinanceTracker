using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.Configurations.Options;
using FinanceTracker.Application.UseCases.Transfer.Notifications;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transfer.Commands.CancelTransfer;

public sealed class CancelTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider,
	IOptionsMonitor<CancellationOptions> options,
	ILogger<CancelTransferHandler> logger
) : IAuthorizedHandler<CancelTransferCommand, Core.Domains.Transfer.Transfer, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		CancelTransferCommand command,
		Core.Domains.Transfer.Transfer transfer,
		CancellationToken ct = default)
	{
		bool creditHadLanded = transfer.Status is TransferStatus.Completed;
		DateTimeOffset occurredAt = dateProvider.UtcNow;

		Result<Unit, DomainException> cancelled = transfer.Cancel(
			cancelledAt: occurredAt,
			maxAge: TimeSpan.FromDays(value: options.CurrentValue.MaxAgeDays)
		);

		if (cancelled.IsFailure)
			return Result<Guid, AppException>.Failure(error: cancelled.Error!);

		Core.Domains.Account.Account? fromAccount = await accountRepository.GetByIdAsync(accountId: transfer.FromAccountId, ct: ct);
		if (fromAccount is null)
			return Result<Guid, AppException>.Failure(error: new NotFoundException(message: $"Account {transfer.FromAccountId} was not found.", id: transfer.FromAccountId));

		Core.Domains.Account.Account? toAccount = null;

		if (creditHadLanded)
		{
			toAccount = await accountRepository.GetByIdAsync(accountId: transfer.ToAccountId, ct: ct);
			if (toAccount is null)
				return Result<Guid, AppException>.Failure(error: new NotFoundException(message: $"Account {transfer.ToAccountId} was not found.", id: transfer.ToAccountId));

			Result<Unit, DomainException> creditReverted = toAccount.RevertTransferCredit(
				occurredAt: occurredAt,
				transferId: transfer.Id,
				amount: transfer.AmountFrom.Amount,
				exchangeRate: transfer.ExchangeRate,
				description: transfer.Description
			);

			if (creditReverted.IsFailure)
				return Result<Guid, AppException>.Failure(error: creditReverted.Error!);
		}

		Result<Unit, DomainException> debitReverted = fromAccount.RevertTransferDebit(
			occurredAt: occurredAt,
			transferId: transfer.Id,
			amount: transfer.AmountFrom.Amount,
			description: transfer.Description
		);

		if (debitReverted.IsFailure)
			return Result<Guid, AppException>.Failure(error: debitReverted.Error!);

		Guid reversalId = Guid.CreateVersion7();

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await accountRepository.SaveAsync(account: fromAccount, ct: ct);

			if (toAccount is not null)
				await accountRepository.SaveAsync(account: toAccount, ct: ct);

			await transferWriteRepository.CancelAsync(
				transfer: transfer,
				reversalId: reversalId,
				occurredAt: occurredAt,
				ct: ct
			);
		},
		onError: exception =>
		{
			logger.ZLogError(exception: exception, message: $"Failed to cancel transfer {transfer.Id}.");
			return Task.CompletedTask;
		},
		ct: ct);

		postCommitNotifications.Stage(notification: new TransferCancelledNotification(
			TransferId: transfer.Id,
			UserId: transfer.UserId,
			FromAccountId: transfer.FromAccountId,
			ToAccountId: transfer.ToAccountId,
			ReversalId: reversalId,
			AmountFrom: transfer.AmountFrom.Amount,
			CurrencyFrom: transfer.AmountFrom.Currency.Value,
			AmountTo: transfer.AmountTo.Amount,
			CurrencyTo: transfer.AmountTo.Currency.Value,
			CreditHadLanded: creditHadLanded,
			OccurredAt: occurredAt
		));

		return Result<Guid, AppException>.Success(value: transfer.Id);
	}
}
