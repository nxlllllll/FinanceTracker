using FinanceTracker.Application.UseCases.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.UseCases.Transactions.Services;

public sealed class TransactionCreationService(
    IAccountRepository accountRepository,
    ITransactionWriteRepository transactionWriteRepository,
    ICurrencyConversionService currencyConversionService,
    IUnitOfWork unitOfWork,
    ICategoryTotalWriteRepository categoryTotalWriteRepository,
    IBudgetProgressWriteRepository budgetProgressWriteRepository,
    IDateProvider dateProvider
) : ITransactionCreationService
{
    private Result<Unit, DomainException> ApplyDirection(
        Account account,
        CreateTransactionCommand command,
        Guid transactionId,
        decimal rate,
        DateTime occurredAt)
    {
        return command.Direction switch
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
    }

    public async Task<Result<Guid, DomainException>> CreateAsync(
        CreateTransactionCommand command,
        Account account,
        CancellationToken ct = default)
    {
        DateTime now = dateProvider.UtcNow;
        Result<Currency, DomainException> fromCurrencyResult = Currency.Create(value: command.Currency);
        if (fromCurrencyResult.IsFailure)
            return Result<Guid, DomainException>.Failure(error: fromCurrencyResult.Error!);
        
        ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
            fromCurrency: fromCurrencyResult.Value,
            toCurrency: account.Currency,
            date: DateOnly.FromDateTime(dateTime: command.OccurredAt),
            ct: ct
        );

        Result<Money, DomainException> amountResult = Money.Create(amount: command.Amount, currency: fromCurrencyResult.Value);
        if (amountResult.IsFailure)
            return Result<Guid, DomainException>.Failure(error: amountResult.Error!);

        Transaction transaction = Transaction.Create(
            accountId: command.AccountId,
            userId: command.UserId,
            categoryId: command.CategoryId,
            amount: amountResult.Value,
            direction: command.Direction,
            exchangeRate: conversion.Rate,
            isRatePending: conversion.IsPending,
            description: command.Description,
            occurredAt: now
        );

        Result<Unit, DomainException> result = ApplyDirection(
            account: account,
            command: command,
            transactionId: transaction.Id,
            rate: conversion.Rate,
            occurredAt: now
        );
        if (result.IsFailure)
            return Result<Guid, DomainException>.Failure(error: result.Error!);

        await unitOfWork.ExecuteInTransactionAsync(operation: async () =>
        {
            await transactionWriteRepository.CreateAsync(transaction: transaction, ct: ct);

            await accountRepository.SaveAsync(account: account, ct: ct);

            if (command.Direction != DirectionType.Debit)
                return;

            await categoryTotalWriteRepository.AddAsync(
                userId: command.UserId,
                categoryId: command.CategoryId,
                currency: command.Currency,
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
        }, ct: ct);

        return Result<Guid, DomainException>.Success(value: transaction.Id);
    }
}