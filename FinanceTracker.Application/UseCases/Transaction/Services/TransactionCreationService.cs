using FinanceTracker.Application.UseCases.Transaction.Commands.CreateTransaction;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.ValueObjects;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace FinanceTracker.Application.UseCases.Transaction.Services;

public sealed class TransactionCreationService(
    IAccountRepository accountRepository,
    ITransactionWriteRepository transactionWriteRepository,
    ICurrencyConversionService currencyConversionService,
    IUnitOfWork unitOfWork,
    ICategoryTotalWriteRepository categoryTotalWriteRepository,
    IBudgetProgressWriteRepository budgetProgressWriteRepository,
    ILogger<TransactionCreationService> logger
) : ITransactionCreationService
{
    private Result<Unit, DomainException> ApplyDirection(
        Core.Domains.Account.Account account,
        CreateTransactionCommand command,
        Guid transactionId,
        decimal rate,
        DateTimeOffset occurredAt)
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
        Core.Domains.Account.Account account,
        CancellationToken ct = default)
    {
        Result<Money, DomainException> amountResult = Money.Create(amount: command.Amount, currency: command.Currency);
        if (amountResult.IsFailure)
            return Result<Guid, DomainException>.Failure(error: amountResult.Error!);
        
        ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
            fromCurrency: command.Currency,
            toCurrency: account.Currency,
            date: DateOnly.FromDateTime(dateTime: command.OccurredAt.UtcDateTime),
            ct: ct
        );
        
        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Create(
            accountId: command.AccountId,
            userId: command.UserId,
            categoryId: command.CategoryId,
            amount: amountResult.Value,
            direction: command.Direction,
            exchangeRate: conversion.Rate,
            isRatePending: conversion.IsPending,
            description: command.Description,
            occurredAt: command.OccurredAt
        );

        Result<Unit, DomainException> result = ApplyDirection(
            account: account,
            command: command,
            transactionId: transaction.Id,
            rate: conversion.Rate,
            occurredAt: command.OccurredAt
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
        },
        onError: async exception => logger.ZLogError(exception: exception, message: $"Failed to create transaction for account {account.Id}."),
        ct: ct);

        return Result<Guid, DomainException>.Success(value: transaction.Id);
    }
}