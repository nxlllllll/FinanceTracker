using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Transaction;

/// <remarks>
/// Transaction metadata (CategoryId, Description, IsExcluded) is managed via CRUD
/// and is NOT recorded in the Event Store. This is intentional. A full ES rebuild
/// restores only financial data (Amount, Direction, ExchangeRate) from Account events.
/// If metadata rebuild is ever required, these fields must be sourced separately.
/// </remarks>
public sealed class TransactionWriteRepository(
	FinanceTrackerContext context
) : ITransactionWriteRepository
{
    public async Task CreateAsync(
        Guid transactionId,
        Guid accountId,
        Guid userId,
        Guid categoryId,
        decimal amount,
        DirectionType direction,
        decimal exchangeRate,
        string? description,
        DateTime occurredAt,
        bool isRatePending,
        CancellationToken ct = default)
    {
        await context.Transactions.AddAsync(entity: new TransactionEntity()
        {
            Id = transactionId,
            AccountId = accountId,
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            Direction = direction,
            ExchangeRate = exchangeRate,
            Description = description,
            IsExcluded = false,
            IsRatePending = isRatePending,
            OccurredAt = occurredAt
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
}