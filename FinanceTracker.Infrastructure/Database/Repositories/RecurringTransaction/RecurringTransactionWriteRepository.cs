using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.RecurringTransaction;

public sealed class RecurringTransactionWriteRepository(
    FinanceTrackerContext context,
    IDateProvider dateProvider
) : IRecurringTransactionWriteRepository
{
    public async Task CreateAsync(
        Core.Domains.RecurringTransaction.RecurringTransaction recurringTransaction,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.AddAsync(entity: new RecurringTransactionEntity()
        {
            Id = recurringTransaction.Id,
            UserId = recurringTransaction.UserId,
            AccountId = recurringTransaction.AccountId,
            CategoryId = recurringTransaction.CategoryId,
            Amount = recurringTransaction.Amount.Amount,
            Currency = recurringTransaction.Amount.Currency,
            Direction = recurringTransaction.Direction,
            DayOfMonth = recurringTransaction.DayOfMonth,
            Description = recurringTransaction.Description,
            IsActive = true,
            LastExecutedAt = null,
            CreatedAt = dateProvider.UtcNow
        }, cancellationToken: ct);

        await context.SaveChangesAsync(cancellationToken: ct);
    }

    public async Task ChangeAmountAsync(
        Guid recurringTransactionId,
        decimal amount,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.Amount,
                valueExpression: amount
            ),
            cancellationToken: ct
        );
    }

    public async Task ChangeCurrencyAsync(
        Guid recurringTransactionId,
        Core.ValueObjects.Currency currency,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.Currency,
                valueExpression: currency
            ),
            cancellationToken: ct
        );
    }

    public async Task ChangeDayOfMonthAsync(
        Guid recurringTransactionId,
        int dayOfMonth,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.DayOfMonth,
                valueExpression: dayOfMonth
            ),
            cancellationToken: ct
        );
    }

    public async Task ActivateAsync(
        Guid recurringTransactionId,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.IsActive,
                valueExpression: true
            ),
            cancellationToken: ct
        );
    }

    public async Task DeactivateAsync(
        Guid recurringTransactionId,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.IsActive,
                valueExpression: false
            ),
            cancellationToken: ct
        );
    }

    
    public async Task DeactivateByCategoryIdAsync(
        Guid categoryId,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.Where(predicate: r => r.CategoryId == categoryId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.IsActive,
                valueExpression: false
            ),
            cancellationToken: ct
        );
    }
    
    public async Task MarkExecutedAsync(
        Guid recurringTransactionId,
        DateTimeOffset executedAt,
        CancellationToken ct = default)
    {
        await context.RecurringTransactions.Where(predicate: r => r.Id == recurringTransactionId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(
                propertyExpression: r => r.LastExecutedAt,
                valueExpression: executedAt
            ),
            cancellationToken: ct
        );
    }
}
