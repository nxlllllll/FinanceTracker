using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Authorization;
using FinanceTracker.Application.UseCases.Transfer.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using MediatR;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.UseCases.Transfer.Commands;

public sealed class CreateTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork,
	IPublisher publisher,
	ILogger<CreateTransferHandler> logger
) : IAuthorizedHandler<CreateTransferCommand, TransferAccounts, Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateTransferCommand command,
		TransferAccounts accounts,
		CancellationToken ct = default)
	{
		Core.Domains.Account.Account account = accounts.FromAccount;

		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: account.Currency,
			toCurrency: accounts.ToAccountCurrency,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt.UtcDateTime),
			ct: ct
		);

		Result<Core.Domains.Transfer.Transfer, DomainException> transferResult = Core.Domains.Transfer.Transfer.Create(
			userId: command.UserId,
			fromAccountId: command.FromAccountId,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			currencyFrom: account.Currency,
			currencyTo: accounts.ToAccountCurrency,
			exchangeRate: conversion.Rate,
			isRatePending: conversion.IsPending,
			description: command.Description,
			occurredAt: command.OccurredAt
		);
		if (transferResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: transferResult.Error!);

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
			return Result<Guid, DomainException>.Failure(error: debitResult.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transferWriteRepository.CreateAsync(transfer: transfer, ct: ct);
			await accountRepository.SaveAsync(account: account, ct: ct);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to debit transfer {account.Id} > {command.ToAccountId}."),
		ct: ct);

		try
		{
			await publisher.Publish(notification: new TransferCreatedNotification(
				TransferId: transfer.Id,
				UserId: transfer.UserId,
				FromAccountId: transfer.FromAccountId,
				ToAccountId: transfer.ToAccountId,
				AmountFrom: transfer.AmountFrom.Amount,
				CurrencyFrom: transfer.AmountFrom.Currency,
				AmountTo: transfer.AmountTo.Amount,
				CurrencyTo: transfer.AmountTo.Currency,
				ExchangeRate: transfer.ExchangeRate,
				IsRatePending: transfer.IsRatePending,
				Description: transfer.Description,
				OccurredAt: transfer.OccurredAt
			), cancellationToken: ct);
		}
		catch (Exception ex)
		{
			logger.ZLogError(exception: ex, message: $"Failed to publish TransferCreatedNotification for transfer {transfer.Id} after successful commit.");
		}

		return Result<Guid, DomainException>.Success(value: transfer.Id);
	}
}
