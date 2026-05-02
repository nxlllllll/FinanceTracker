using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;

public sealed class BudgetProgressWriteRepository(
    FinanceTrackerContext context,
    ICurrencyConversionService currencyConversionService,
    IDateProvider dateProvider
) : IBudgetProgressWriteRepository
{
    private async Task ChangeSpentAsync(
        Guid userId,
        Guid categoryId,
        string currencyCode,
        decimal amount,
        DateTime occurredAt,
        int delta,
        CancellationToken ct)
    {
        DateOnly date = DateOnly.FromDateTime(dateTime: occurredAt);

        List<BudgetEntity> budgets = await context.Budgets.AsNoTracking().Where(predicate: b =>
            b.UserId == userId &&
            b.CategoryId == categoryId &&
            b.From <= date &&
            b.To >= date
        ).ToListAsync(cancellationToken: ct);

        foreach (BudgetEntity budget in budgets)
        {
            ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
                fromCurrency: currencyCode,
                toCurrency: budget.Currency,
                date: date,
                ct: ct
            );
            
            await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budget.Id).ExecuteUpdateAsync(
                setPropertyCalls: builder => builder
                    .SetProperty(
                        propertyExpression: p => p.Spent,
                        valueExpression: p => p.Spent + amount * conversion.Rate * delta
                    )
                    .SetProperty(
                        propertyExpression: p => p.UpdatedAt,
                        valueExpression: dateProvider.UtcNow
                    ),
                cancellationToken: ct
            );
        }
    }

    public Task AddAsync(
        Guid userId,
        Guid categoryId,
        string currencyCode,
        decimal amount,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        return ChangeSpentAsync(
            userId: userId,
            categoryId: categoryId,
            currencyCode: currencyCode,
            amount: amount,
            occurredAt: occurredAt,
            delta: 1,
            ct: ct
        );
    }

    public Task SubtractAsync(
        Guid userId,
        Guid categoryId,
        string currencyCode,
        decimal amount,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        return ChangeSpentAsync(
            userId: userId,
            categoryId: categoryId,
            currencyCode: currencyCode,
            amount: amount,
            occurredAt: occurredAt,
            delta: -1,
            ct: ct
        );
    }

    public async Task ChangeCategoryAsync(
        Guid userId,
        Guid oldCategoryId,
        Guid newCategoryId,
        string currencyCode,
        decimal amount,
        DateTime occurredAt,
        CancellationToken ct = default)
    {
        await ChangeSpentAsync(
            userId: userId,
            categoryId: oldCategoryId,
            currencyCode: currencyCode,
            amount: amount,
            occurredAt: occurredAt,
            delta: -1,
            ct: ct
        );

        await ChangeSpentAsync(
            userId: userId,
            categoryId: newCategoryId,
            currencyCode: currencyCode,
            amount: amount,
            occurredAt: occurredAt,
            delta: 1,
            ct: ct
        );
    }

    public async Task RecalculateForBudgetAsync(
        Guid budgetId,
        Guid userId,
        Guid categoryId,
        DateOnly from,
        DateOnly to,
        CancellationToken ct = default)
    {
        BudgetEntity? budget = await context.Budgets.AsNoTracking()
            .FirstOrDefaultAsync(predicate: b => b.Id == budgetId, cancellationToken: ct);
 
        if (budget is null)
            return;
 
        DateTime fromUtc = from.ToDateTime(time: TimeOnly.MinValue, kind: DateTimeKind.Utc);
        DateTime toUtc = to.ToDateTime(time: TimeOnly.MaxValue, kind: DateTimeKind.Utc);
 
        List<TransactionEntity> transactions = await context.Transactions.AsNoTracking().Where(predicate: t =>
            t.UserId == userId && t.CategoryId == categoryId && !t.IsExcluded && t.Direction == DirectionType.Debit && t.OccurredAt >= fromUtc && t.OccurredAt <= toUtc
        ).ToListAsync(cancellationToken: ct);
 
        decimal spent = 0;
 
        foreach (TransactionEntity transaction in transactions)
        {
            ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
                fromCurrency: transaction.Currency,
                toCurrency: budget.Currency,
                date: DateOnly.FromDateTime(dateTime: transaction.OccurredAt),
                ct: ct
            );
 
            spent += transaction.Amount * conversion.Rate;
        }
 
        await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budgetId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder.SetProperty(propertyExpression: p => p.Spent, valueExpression: spent)
                .SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: dateProvider.UtcNow),
            cancellationToken: ct
        );
    }
}