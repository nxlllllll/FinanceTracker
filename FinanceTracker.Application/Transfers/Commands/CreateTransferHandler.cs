using FinanceTracker.Application.Behaviours.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.CurrencyConversion;

namespace FinanceTracker.Application.Transfers.Commands;

public sealed class CreateTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork
) : IAuthorizedHandler<CreateTransferCommand, (Account, Account), Guid>
{
	public async Task<Guid> HandleAsync(
		CreateTransferCommand command,
		(Account, Account) accounts,
		CancellationToken ct = default)
	{
		(Account fromAccount, Account toAccount) = accounts;
		
		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: fromAccount.Currency,
			toCurrency: toAccount.Currency,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt),
			ct: ct
		);

		Transfer transfer = Transfer.Create(
			userId: command.UserId,
			fromAccountId: command.FromAccountId,
			toAccountId: command.ToAccountId,
			amountFrom: command.Amount,
			amountTo: command.Amount * conversion.Rate,
			exchangeRate: conversion.Rate,
			isRatePending: conversion.IsPending,
			description: command.Description,
			occurredAt: command.OccurredAt
		);

		fromAccount.DebitTransfer(
			transferId: transfer.Id,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			forexRate: conversion.Rate,
			description: command.Description
		);

		toAccount.CreditTransfer(
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