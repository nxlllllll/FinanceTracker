using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.User;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Services.CurrencyConversion;
using MediatR;

namespace FinanceTracker.Application.Transactions.Commands.CreateTransaction;

public sealed class CreateTransactionHandler(
	IAccountRepository accountRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository
) : IRequestHandler<CreateTransactionCommand, Guid>
{
	private void ApplyDirection(
		Account account,
		CreateTransactionCommand command,
		Guid transactionId,
		decimal rate)
	{
		switch (command.Direction)
		{
			case DirectionType.Debit:
				account.Debit(
					transactionId: transactionId, 
					categoryId: command.CategoryId,
					amount: command.Amount,
					exchangeRate: rate,
					description: command.Description
				); break;
			case DirectionType.Credit: 
				account.Credit(
					transactionId: transactionId, 
					categoryId: command.CategoryId,
					amount: command.Amount,
					exchangeRate: rate, 
					description: command.Description
				); break;
			default:
				throw new ArgumentOutOfRangeException(message: "Direction is unknown.", paramName: nameof(command.Direction));
		}
	}
	
	public async Task<Guid> Handle(
		CreateTransactionCommand command,
		CancellationToken ct = default)
	{
		Account account = await accountRepository.GetByIdAsync(accountId: command.AccountId, ct: ct) 
			?? throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		if (account.UserId != command.UserId)
			throw new NotFoundException(message: "Account not found.", id: command.AccountId);

		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: command.Currency,
			toCurrency: account.Currency,
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

		await unitOfWork.BeginTransactionAsync(ct: ct);

		try
		{
			await transactionWriteRepository.CreateAsync(
				transactionId: transactionId,
				accountId: command.AccountId,
				userId: command.UserId,
				categoryId: command.CategoryId,
				amount: command.Amount,
				currency: command.Currency,
				direction: command.Direction,
				exchangeRate: conversion.Rate,
				description: command.Description,
				occurredAt: command.OccurredAt,
				isRatePending: conversion.IsPending,
				ct: ct
			);

			await accountRepository.SaveAsync(account: account, ct: ct);

			if (command.Direction == DirectionType.Debit)
			{
				await categoryTotalWriteRepository.AddAsync(
					userId: command.UserId,
					categoryId: command.CategoryId,
					amount: command.Amount,
					occurredAt: command.OccurredAt,
					ct: ct
				);

				await budgetProgressWriteRepository.AddAsync(
					userId: command.UserId,
					categoryId: command.CategoryId,
					currencyCode: command.Currency,
					amount: command.Amount,
					occurredAt: command.OccurredAt,
					ct: ct
				);
			}
			
			await unitOfWork.CommitAsync(ct: ct);
		}
		catch
		{
			await unitOfWork.RollbackAsync(ct: ct);
			throw;
		}

		return transactionId;
	}
}