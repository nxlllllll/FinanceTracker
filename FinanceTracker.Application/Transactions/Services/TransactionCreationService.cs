using FinanceTracker.Application.Transactions.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Application.Transactions.Services;

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
    private void ApplyDirection(
        Account account,
        CreateTransactionCommand command,
        Guid transactionId,
        decimal rate,
        DateTime occurredAt)
    {
        switch (command.Direction)
        {
            case DirectionType.Debit:
                account.Debit(
                    occurredAt: occurredAt,
                    transactionId: transactionId,
                    categoryId: command.CategoryId,
                    amount: command.Amount,
                    exchangeRate: rate,
                    description: command.Description
                ); break;
            case DirectionType.Credit:
                account.Credit(
                    occurredAt: occurredAt,
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

    public async Task<Guid> CreateAsync(
        CreateTransactionCommand command,
        Account account,
        CancellationToken ct = default)
    {
        DateTime now = dateProvider.UtcNow;
        ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
            fromCurrency: command.Currency,
            toCurrency: account.Currency,
            date: DateOnly.FromDateTime(dateTime: command.OccurredAt),
            ct: ct
        );

        Transaction transaction = Transaction.Create(
            accountId: command.AccountId,
            userId: command.UserId,
            categoryId: command.CategoryId,
            amount: new Money(amount: command.Amount, currency: command.Currency),
            direction: command.Direction,
            exchangeRate: conversion.Rate,
            isRatePending: conversion.IsPending,
            description: command.Description,
            occurredAt: now
        );

        ApplyDirection(
            account: account,
            command: command,
            transactionId: transaction.Id,
            rate: conversion.Rate,
            occurredAt: now
        );

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

        return transaction.Id;
    }
}