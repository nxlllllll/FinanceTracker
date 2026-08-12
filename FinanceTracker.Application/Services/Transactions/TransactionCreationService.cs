using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Application.UseCases.Transaction.Utilities;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using ZLogger;
using Unit = FinanceTracker.Core.Results.Unit;

namespace FinanceTracker.Application.Services.Transactions;

public sealed class TransactionCreationService(
	IAccountRepository accountRepository,
	ICategoryReadRepository categoryReadRepository,
	ITransactionWriteRepository transactionWriteRepository,
	ICurrencyConversionService currencyConversionService,
	IUnitOfWork unitOfWork,
	ICategoryTotalWriteRepository categoryTotalWriteRepository,
	IBudgetProgressWriteRepository budgetProgressWriteRepository,
	IDateProvider dateProvider,
	ILogger<TransactionCreationService> logger
) : ITransactionCreationService
{
	private Result<Unit, DomainException> ApplyDirection(
		Core.Domains.Account.Account account,
		CreateTransactionCommand command,
		Guid transactionId,
		decimal rate,
		DateTimeOffset occurredAt
	) => command.Direction switch
	{
		DirectionType.Debit => account.Debit(
			occurredAt: occurredAt,
			transactionId: transactionId,
			categoryId: command.CategoryId,
			amount: command.Amount,
			exchangeRate: rate,
			description: command.Description
		),
		DirectionType.Credit => account.Credit(
			occurredAt: occurredAt,
			transactionId: transactionId,
			categoryId: command.CategoryId,
			amount: command.Amount,
			exchangeRate: rate,
			description: command.Description
		),
		_ => throw new InvalidTransactionDirectionException(message: "Direction is unknown.")
	};

	private async Task<DomainException?> ValidateCategoryAsync(
		CreateTransactionCommand command,
		CancellationToken ct)
	{
		CategoryReadModel? category = await categoryReadRepository.GetByIdAsync(
			categoryId: command.CategoryId,
			userId: command.UserId,
			ct: ct
		);

		if (category is null)
			return new NotFoundException(message: "Category not found.", id: command.CategoryId);

		if (category.IsArchived)
			return new ArchivedOperationException(message: "Cannot create a transaction for an archived category.");

		return CategoryDirectionValidator.Validate(category: category, direction: command.Direction);
	}

	public async Task<Result<Core.Domains.Transaction.Transaction, DomainException>> CreateAsync(
		CreateTransactionCommand command,
		Core.Domains.Account.Account account,
		CancellationToken ct = default)
	{
		DomainException? categoryError = await ValidateCategoryAsync(command: command, ct: ct);
		if (categoryError is not null)
			return Result<Core.Domains.Transaction.Transaction, DomainException>.Failure(error: categoryError);

		Result<Money, DomainException> amountResult = Money.Create(amount: command.Amount, currency: command.Currency);
		if (amountResult.IsFailure)
			return Result<Core.Domains.Transaction.Transaction, DomainException>.Failure(error: amountResult.Error!);

		ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
			fromCurrency: command.Currency,
			toCurrency: account.Currency,
			date: DateOnly.FromDateTime(dateTime: command.OccurredAt.UtcDateTime),
			ct: ct
		);

		Result<Core.Domains.Transaction.Transaction, DomainException> transactionResult = Core.Domains.Transaction.Transaction.Create(
			createdAt: dateProvider.UtcNow,
			occurredAt: command.OccurredAt,
			accountId: command.AccountId,
			userId: command.UserId,
			categoryId: command.CategoryId,
			amount: amountResult.Value,
			baseCurrency: account.Currency,
			direction: command.Direction,
			exchangeRate: conversion.Rate,
			rateStatus: conversion.Status,
			description: command.Description
		);

		if (transactionResult.IsFailure)
			return Result<Core.Domains.Transaction.Transaction, DomainException>.Failure(error: transactionResult.Error!);

		Core.Domains.Transaction.Transaction transaction = transactionResult.Value!;

		Result<Unit, DomainException> result = ApplyDirection(
			account: account,
			command: command,
			transactionId: transaction.Id,
			rate: conversion.Rate,
			occurredAt: command.OccurredAt
		);
		if (result.IsFailure)
			return Result<Core.Domains.Transaction.Transaction, DomainException>.Failure(error: result.Error!);

		await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
		{
			await transactionWriteRepository.CreateAsync(transaction: transaction, ct: ct);
			await accountRepository.SaveAsync(account: account, ct: ct);

			await categoryTotalWriteRepository.AddAsync(
				userId: command.UserId,
				categoryId: command.CategoryId,
				currency: command.Currency,
				amount: command.Amount,
				occurredAt: command.OccurredAt,
				ct: ct
			);

			if (command.Direction != DirectionType.Debit)
				return;

			await budgetProgressWriteRepository.AddAsync(
				userId: command.UserId,
				categoryId: command.CategoryId,
				currencyCode: command.Currency,
				amount: command.Amount,
				occurredAt: command.OccurredAt,
				ct: ct
			);
		},
		onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to create transaction for account {account.Id}."),
		ct: ct);

		return Result<Core.Domains.Transaction.Transaction, DomainException>.Success(value: transaction);
	}
}
