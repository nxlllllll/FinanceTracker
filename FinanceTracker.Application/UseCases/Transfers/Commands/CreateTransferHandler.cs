using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transfers.Commands;

public sealed class CreateTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider,
	ILogger<CreateTransferHandler> logger
) : IAuthorizedHandler<CreateTransferCommand, (Account, Account), Guid, DomainException>
{
	public async Task<Result<Guid, DomainException>> HandleAsync(
		CreateTransferCommand command,
		(Account, Account) accounts,
		CancellationToken ct = default)
	{
		(Account fromAccount, Account toAccount) = accounts;
		Result<Currency, DomainException> fromCurrencyResult = Currency.Create(value: command.CurrencyFrom);
		if (fromCurrencyResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: fromCurrencyResult.Error!);

		Result<Currency, DomainException> toCurrencyResult = Currency.Create(value: command.CurrencyTo);
		if (toCurrencyResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: toCurrencyResult.Error!);

		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: fromCurrencyResult.Value,
			toCurrency: toCurrencyResult.Value,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt),
			ct: ct
		);

		Transfer transfer = Transfer.Create(
			userId: command.UserId,
			fromAccountId: command.FromAccountId,
			toAccountId: command.ToAccountId,
			amountFrom: command.Amount,
			currencyFrom: fromCurrencyResult.Value,
			amountTo: command.Amount * conversion.Rate,
			currencyTo: toCurrencyResult.Value,
			exchangeRate: conversion.Rate,
			isRatePending: conversion.IsPending,
			description: command.Description,
			occurredAt: command.OccurredAt
		);

		DateTime now = dateProvider.UtcNow;

		Result<Unit, DomainException> debitResult = fromAccount.DebitTransfer(
			occurredAt: now,
			transferId: transfer.Id,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			forexRate: conversion.Rate,
			description: command.Description
		);
		if (debitResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: debitResult.Error!);

		Result<Unit, DomainException> creditResult = toAccount.CreditTransfer(
			occurredAt: now,
			transferId: transfer.Id,
			fromAccountId: command.FromAccountId,
			amount: command.Amount,
			exchangeRate: conversion.Rate,
			description: command.Description
		);
		if (creditResult.IsFailure)
			return Result<Guid, DomainException>.Failure(error: creditResult.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transferWriteRepository.CreateAsync(transfer: transfer, ct: ct);
			await accountRepository.SaveAsync(account: fromAccount, ct: ct);
			await accountRepository.SaveAsync(account: toAccount, ct: ct);
		}, 
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to create transfer {fromAccount.Id} → {toAccount.Id}."),
		ct: ct);

		return Result<Guid, DomainException>.Success(value: transfer.Id);
	}
}