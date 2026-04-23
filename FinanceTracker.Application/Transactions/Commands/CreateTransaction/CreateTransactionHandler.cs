using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Services.CurrencyConversion;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	IAccountRepository accountRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUserRepository userRepository
) : IRequestHandler<CreateTransactionCommand, Guid>
{
	private void ApplyDirection(
		Account account,
		CreateTransactionCommand command,
		Guid transactionId,
		decimal rate)
	{
		Action<Guid, Guid, decimal, decimal, string?> func = command.Direction switch
		{
			DirectionType.Debit => account.Debit,
			DirectionType.Credit => account.Credit,
			_ => throw new ArgumentOutOfRangeException(message: "Direction is unknown.", paramName: nameof(command.Direction))
		};

		func(transactionId, command.CategoryId, command.Amount, rate, command.Description);
	}
	
	public async Task<Guid> Handle(
		CreateTransactionCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: command.AccountId, ct: ct) 
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		User user = await userRepository.GetByIdAsync(userId: command.UserId, ct: ct) 
			?? throw new NotFoundException(message: "User not found.", id: command.UserId);

		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: account.Currency,
			toCurrency: user.BaseCurrencyCode,
			date: DateOnly.FromDateTime(command.OccurredAt),
			ct: ct
		);

		Guid transactionId = Guid.NewGuid();
		ApplyDirection(
			account: account,
			command: command,
			transactionId: transactionId,
			rate: conversion.Rate
		);

		await transactionWriteRepository.CreateAsync(
			transactionId: transactionId,
			accountId: command.AccountId,
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: command.Amount,
			direction: command.Direction,
			exchangeRate: conversion.Rate,
			description: command.Description,
			occurredAt: command.OccurredAt,
			isRatePending: conversion.IsPending,
			ct: ct
		);

		await accountRepository.SaveAsync(account: account, ct: ct);

		return transactionId;
	}
}