using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

public sealed class TransactionWriteRepository(
	FinanceTrackerContext context
) : ITransactionWriteRepository
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
            OccurredAt = transaction.OccurredAt
        }, cancellationToken: ct);

        await context.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task ChangeCategoryAsync(
        Guid transactionId,
        Guid categoryId,
        CancellationToken ct = default)
    {
        await context.Transactions.Where(predicate: t => t.Id == transactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: e => e.CategoryId,
                valueExpression: categoryId),
            cancellationToken: ct
        );
    }

    public async Task ChangeDescriptionAsync(
        Guid transactionId,
        string? description,
        CancellationToken ct = default)
    {
        await context.Transactions.Where(predicate: t => t.Id == transactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: e => e.Description,
                valueExpression: description),
            cancellationToken: ct
        );
    }

    public async Task IncludeAsync(
        Guid transactionId,
        CancellationToken ct = default)
    {
        await context.Transactions.Where(predicate: t => t.Id == transactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: e => e.IsExcluded,
                valueExpression: false),
            cancellationToken: ct
        );
    }

    public async Task ExcludeAsync(
        Guid transactionId,
        CancellationToken ct = default)
    {
        await context.Transactions.Where(predicate: t => t.Id == transactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: e => e.IsExcluded,
                valueExpression: true),
            cancellationToken: ct
        );
    }

    public async Task UpdateRateAsync(
        Guid transactionId,
        decimal newRate,
        CancellationToken ct = default)
    {
        await context.Transactions.Where(predicate: t => t.Id == transactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(propertyExpression: t => t.ExchangeRate, valueExpression: newRate)
                .SetProperty(propertyExpression: t => t.IsRatePending, valueExpression: false),
            cancellationToken: ct
        );
    }
}