using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Transfer.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transfer.Commands.CreateTransfer;

public sealed class CreateTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork,
	IPostCommitNotifications postCommitNotifications,
	IDateProvider dateProvider,
	ILogger<CreateTransferHandler> logger
) : IAuthorizedHandler<CreateTransferCommand, TransferAccount, Guid, AppException>
{
	public async Task<Result<Guid, AppException>> HandleAsync(
		CreateTransferCommand command,
		TransferAccount transferAccount,
		CancellationToken ct = default)
	{
		Core.Domains.Account.Account account = transferAccount.FromAccount;

		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: account.Currency,
			toCurrency: transferAccount.ToAccountCurrency,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt.UtcDateTime),
			ct: ct
		);

		Result<Core.Domains.Transfer.Transfer, DomainException> transferResult = Core.Domains.Transfer.Transfer.Create(
			createdAt: dateProvider.UtcNow,
			userId: command.UserId,
			fromAccountId: command.FromAccountId,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			currencyFrom: account.Currency,
			currencyTo: transferAccount.ToAccountCurrency,
			exchangeRate: conversion.Rate,
			rateStatus: conversion.Status,
			description: command.Description,
			occurredAt: command.OccurredAt
		);
		if (transferResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: transferResult.Error!);

		Core.Domains.Transfer.Transfer transfer = transferResult.Value!;

		Result<Unit, DomainException> debitResult = account.DebitTransfer(
			occurredAt: command.OccurredAt,
			transferId: transfer.Id,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			forexRate: conversion.Rate,
			description: command.Description
		);
		if (debitResult.IsFailure)
			return Result<Guid, AppException>.Failure(error: debitResult.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transferWriteRepository.CreateAsync(transfer: transfer, ct: ct);
			await accountRepository.SaveAsync(account: account, ct: ct);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to debit transfer {account.Id} > {command.ToAccountId}."),
		ct: ct);

		postCommitNotifications.Stage(notification: new TransferCreatedNotification(
			TransferId: transfer.Id,
			UserId: transfer.UserId,
			FromAccountId: transfer.FromAccountId,
			ToAccountId: transfer.ToAccountId,
			AmountFrom: transfer.AmountFrom.Amount,
			CurrencyFrom: transfer.AmountFrom.Currency,
			AmountTo: transfer.AmountTo.Amount,
			CurrencyTo: transfer.AmountTo.Currency,
			ExchangeRate: transfer.ExchangeRate,
			RateStatus: transfer.RateStatus,
			Description: transfer.Description,
			OccurredAt: transfer.OccurredAt
		));

		return Result<Guid, AppException>.Success(value: transfer.Id);
	}
}
