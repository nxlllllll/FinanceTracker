using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;

public sealed class BudgetProgressWriteRepository(
    FinanceTrackerContext context,
    ICurrencyConversionService currencyConversionService,
    IDateProvider dateProvider
) : IBudgetProgressWriteRepository
{
    private readonly record struct RateKey(Core.ValueObjects.Currency Currency, DateOnly Date);
    
    private async Task ChangeSpentAsync(
        Guid userId,
        Guid categoryId,
        Core.ValueObjects.Currency currencyCode,
        decimal amount,
        DateTimeOffset occurredAt,
        int delta,
        CancellationToken ct)
    {
        DateOnly date = DateOnly.FromDateTime(dateTime: occurredAt.UtcDateTime);

        List<BudgetEntity> budgets = await context.Budgets.AsNoTracking().Where(predicate: b =>
            b.UserId == userId &&
            b.CategoryId == categoryId &&
            b.From <= date &&
            b.To >= date
        ).ToListAsync(cancellationToken: ct);

        foreach (BudgetEntity budget in budgets)
        {
            decimal additionSpent = amount * delta;
            if (budget.Currency != currencyCode)
            {
                ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
                    fromCurrency: currencyCode,
                    toCurrency: budget.Currency,
                    date: date,
                    ct: ct
                );
                additionSpent *= conversion.Rate;
            }
            
            await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budget.Id).ExecuteUpdateAsync(setPropertyCalls: builder => builder
                .SetProperty(
                    propertyExpression: p => p.Spent,
                    valueExpression: p => p.Spent + additionSpent
                )
                .SetProperty(
                    propertyExpression: p => p.UpdatedAt,
                    valueExpression: dateProvider.UtcNow
                ), cancellationToken: ct
            );
        }
    }

    public Task AddAsync(
        Guid userId,
        Guid categoryId,
        Core.ValueObjects.Currency currencyCode,
        decimal amount,
        DateTimeOffset occurredAt,
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
        Core.ValueObjects.Currency currencyCode,
        decimal amount,
        DateTimeOffset occurredAt,
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
        Core.ValueObjects.Currency currencyCode,
        decimal amount,
        DateTimeOffset occurredAt,
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
        DateOnly fromDate,
        DateOnly toDate,
        CancellationToken ct = default)
    {
        BudgetEntity? budget = await context.Budgets.AsNoTracking()
            .FirstOrDefaultAsync(predicate: b => b.Id == budgetId, cancellationToken: ct);

        if (budget is null)
            return;

        DateTimeOffset fromUtc = fromDate.ToDateTime(time: TimeOnly.MinValue, kind: DateTimeKind.Utc);
        DateTimeOffset toUtc = toDate.ToDateTime(time: TimeOnly.MaxValue, kind: DateTimeKind.Utc);

        List<TransactionEntity> transactions = await context.Transactions.AsNoTracking().Where(predicate: t =>
            t.UserId == userId && t.CategoryId == categoryId && !t.IsExcluded && t.Direction == DirectionType.Debit && t.OccurredAt >= fromUtc && t.OccurredAt <= toUtc
        ).ToListAsync(cancellationToken: ct);

        HashSet<RateKey> uniquePairs = transactions.Select(selector: t => new RateKey(
            Currency: t.Currency, 
            Date: DateOnly.FromDateTime(dateTime: t.OccurredAt.UtcDateTime)
        )).ToHashSet();

        Dictionary<RateKey, decimal> rates = new Dictionary<RateKey, decimal>();
        foreach (RateKey rateKey in uniquePairs)
        {
            ConversionResult conversion = await currencyConversionService.GetConversionRateAsync(
                fromCurrency: rateKey.Currency,
                toCurrency: budget.Currency,
                date: rateKey.Date,
                ct: ct
            );
            rates[rateKey] = conversion.Rate;
        }

        decimal spent = transactions.Sum(selector: t =>
            t.Amount * rates[new RateKey(Currency: t.Currency, Date: DateOnly.FromDateTime(dateTime: t.OccurredAt.UtcDateTime))]
        );

        await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budgetId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: p => p.Spent, valueExpression: spent)
                .SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: dateProvider.UtcNow),
            cancellationToken: ct
        );
    }
}
