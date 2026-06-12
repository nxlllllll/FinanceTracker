using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionWriteRepository(FinanceTrackerContext context) : ITransactionWriteRepository
{
    public async Task CreateAsync(
        Core.Domains.Transaction.Transaction transaction,
        CancellationToken ct = default)
    {
        await context.Transactions.AddAsync(entity: new TransactionEntity()
        {
            Id = transaction.Id,
            AccountId = transaction.AccountId,
            UserId = transaction.UserId,
            CategoryId = transaction.CategoryId,
            Amount = transaction.Amount.Amount,
            Currency = transaction.Amount.Currency,
            Direction = transaction.Direction,
            ExchangeRate = transaction.ExchangeRate,
            Description = transaction.Description,
            IsExcluded = false,
            IsRatePending = transaction.IsRatePending,
            RowVersion = 0,
            OccurredAt = transaction.OccurredAt
        }, cancellationToken: ct);
    }

    public async Task ChangeCategoryAsync(
        Guid transactionId,
        Guid categoryId,
        int expectedVersion,
        CancellationToken ct = default)
    {
        int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: e => e.CategoryId, valueExpression: categoryId)
                .SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
            cancellationToken: ct
        );

        if (affected == 0)
            throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);
    }

    public async Task ChangeDescriptionAsync(
        Guid transactionId,
        string? description,
        int expectedVersion,
        CancellationToken ct = default)
    {
        int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: e => e.Description, valueExpression: description)
                .SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
            cancellationToken: ct
        );

        if (affected == 0)
            throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);
    }

    public async Task IncludeAsync(
        Guid transactionId,
        int expectedVersion,
        CancellationToken ct = default)
    {
        int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: e => e.IsExcluded, valueExpression: false)
                .SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
            cancellationToken: ct
        );

        if (affected == 0)
            throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);
    }

    public async Task ExcludeAsync(
        Guid transactionId,
        int expectedVersion,
        CancellationToken ct = default)
    {
        int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: e => e.IsExcluded, valueExpression: true)
                .SetProperty(propertyExpression: e => e.RowVersion, valueExpression: expectedVersion + 1),
            cancellationToken: ct
        );

        if (affected == 0)
            throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);
    }

    public async Task UpdateRateAsync(
        Guid transactionId,
        decimal newRate,
        int expectedVersion,
        CancellationToken ct = default)
    {
        int affected = await context.Transactions.Where(predicate: t => t.Id == transactionId && t.RowVersion == expectedVersion).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: t => t.ExchangeRate, valueExpression: newRate)
                .SetProperty(propertyExpression: t => t.IsRatePending, valueExpression: false)
                .SetProperty(propertyExpression: t => t.RowVersion, valueExpression: expectedVersion + 1),
            cancellationToken: ct
        );

        if (affected == 0)
            throw new ConcurrencyConflictException(message: $"Transaction {transactionId} was modified by another request.", id: transactionId);
    }
}