using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;

namespace FinanceTracker.Application.Transfers.Commands;

public sealed class CreateTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork,
	IDateProvider dateProvider
) : IAuthorizedHandler<CreateTransferCommand, (Account, Account), Guid>
{
	public async Task<Guid> HandleAsync(
		CreateTransferCommand command,
		(Account, Account) accounts,
		CancellationToken ct = default)
	{
		(Account fromAccount, Account toAccount) = accounts;
		
		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: command.CurrencyFrom,
			toCurrency: command.CurrencyTo,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt),
			ct: ct
		);

		Transfer transfer = Transfer.Create(
			userId: command.UserId,
			fromAccountId: command.FromAccountId,
			toAccountId: command.ToAccountId,
			amountFrom: command.Amount,
			currencyFrom: command.CurrencyFrom,
			amountTo: command.Amount * conversion.Rate,
			currencyTo: command.CurrencyTo,
			exchangeRate: conversion.Rate,
			isRatePending: conversion.IsPending,
			description: command.Description,
			occurredAt: command.OccurredAt
		);

		DateTime now = dateProvider.UtcNow;
		
		fromAccount.DebitTransfer(
			occurredAt: now,
			transferId: transfer.Id,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			forexRate: conversion.Rate,
			description: command.Description
		);

		toAccount.CreditTransfer(
			occurredAt: now,
			transferId: transfer.Id,
			fromAccountId: command.FromAccountId,
			amount: command.Amount,
			exchangeRate: conversion.Rate,
			description: command.Description
		);

		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await transferWriteRepository.CreateAsync(transfer: transfer, ct: ct);

			await accountRepository.SaveAsync(account: fromAccount, ct: ct);
			await accountRepository.SaveAsync(account: toAccount, ct: ct);
			
			await unitOfWork.CommitAsync(ct: ct);
		}
		catch
		{
			await unitOfWork.RollbackAsync(ct: ct);
			throw;
		}

		return transfer.Id;
	}
}