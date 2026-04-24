using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Services.CurrencyConversion;
using MediatR;

namespace FinanceTracker.Application.Transfers.Commands;

public sealed class CreateTransferHandler(
	IAccountRepository accountRepository,
	ITransferWriteRepository transferWriteRepository,
	ICurrencyConversionService currencyConversionService
) : IRequestHandler<CreateTransferCommand, Guid>
{
	public async Task<Guid> Handle(
		CreateTransferCommand command,
		CancellationToken ct = default)
	{
		if (command.FromAccountId == command.ToAccountId)
			throw new InvalidOperationException(message: "Cannot transfer to the same account.");
		
		Account fromAccount = await accountRepository.GetByIdAsync(accountId: command.FromAccountId, ct: ct)
			?? throw new NotFoundException(message: "Source account not found.", id: command.FromAccountId);

		if (fromAccount.UserId != command.UserId)
			throw new NotFoundException(message: "Source account not found.", id: command.FromAccountId);
		
		Account toAccount = await accountRepository.GetByIdAsync(accountId: command.ToAccountId, ct: ct)
			?? throw new NotFoundException(message: "Destination account not found.", id: command.ToAccountId);

		if (toAccount.UserId != command.UserId)
			throw new NotFoundException(message: "Destination account not found.", id: command.ToAccountId);
		
		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: fromAccount.Currency,
			toCurrency: toAccount.Currency,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt),
			ct: ct
		);

		Guid transferId = Guid.NewGuid();
		decimal amountTo = command.Amount * conversion.Rate;

		fromAccount.DebitTransfer(
			transferId: transferId,
			toAccountId: command.ToAccountId,
			amount: command.Amount,
			exchangeRate: conversion.Rate,
			description: command.Description
		);

		toAccount.CreditTransfer(
			transferId: transferId,
			fromAccountId: command.FromAccountId,
			amount: amountTo,
			exchangeRate: 1m,
			description: command.Description
		);

		await transferWriteRepository.CreateAsync(
			transferId: transferId,
			userId: command.UserId,
			fromAccountId: command.FromAccountId,
			toAccountId: command.ToAccountId,
			amountFrom: command.Amount,
			amountTo: amountTo,
			exchangeRate: conversion.Rate,
			description: command.Description,
			occurredAt: command.OccurredAt,
			isRatePending: conversion.IsPending,
			ct: ct
		);

		await accountRepository.SaveAsync(account: fromAccount, ct: ct);
		await accountRepository.SaveAsync(account: toAccount, ct: ct);

		return transferId;
	}
}