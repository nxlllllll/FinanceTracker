using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetProgressWriteRepository(
    FinanceTrackerContext context,
    ICurrencyConversionService currencyConversionService,
    IDateProvider dateProvider
) : IBudgetProgressWriteRepository
{
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

        if (budgets.Count == 0)
            return;

        List<CurrencyRateRequest> rateRequests = budgets.Select(selector: b => new CurrencyRateRequest(From: currencyCode, To: b.Currency, Date: date))
             .Distinct()
             .ToList();

        Dictionary<CurrencyRateRequest, ConversionResult> rates = await currencyConversionService.GetConversionRatesBatchAsync(requests: rateRequests, ct: ct);

        foreach (BudgetEntity budget in budgets)
        {
            decimal additionSpent = amount * delta * rates[new CurrencyRateRequest(From: currencyCode, To: budget.Currency, Date: date)].Rate;

            await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budget.Id).ExecuteUpdateAsync(
                setPropertyCalls: builder => builder
                    .SetProperty(propertyExpression: p => p.Spent, valueExpression: p => p.Spent + additionSpent)
                    .SetProperty(propertyExpression: p => p.RowVersion, valueExpression: p => p.RowVersion + 1)
                    .SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: dateProvider.UtcNow),
                cancellationToken: ct
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
        BudgetEntity? budget = await context.Budgets.AsNoTracking().FirstOrDefaultAsync(predicate: b => b.Id == budgetId, cancellationToken: ct);

        if (budget is null)
            return;

        DateTimeOffset fromUtc = new DateTimeOffset(date: fromDate, time: TimeOnly.MinValue, offset: TimeSpan.Zero);
        DateTimeOffset toUtc = new DateTimeOffset(date: toDate, time: TimeOnly.MaxValue, offset: TimeSpan.Zero);

        List<TransactionEntity> transactions = await context.Transactions.AsNoTracking().Where(predicate: t =>
            t.UserId == userId &&
            t.CategoryId == categoryId &&
            !t.IsExcluded &&
            t.Direction == DirectionType.Debit &&
            t.OccurredAt >= fromUtc &&
            t.OccurredAt <= toUtc
        ).ToListAsync(cancellationToken: ct);

        if (transactions.Count == 0)
        {
            await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budgetId).ExecuteUpdateAsync(
                setPropertyCalls: builder => builder
                    .SetProperty(propertyExpression: p => p.Spent, valueExpression: 0m)
                    .SetProperty(propertyExpression: p => p.RowVersion, valueExpression: p => p.RowVersion + 1)
                    .SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: dateProvider.UtcNow),
                cancellationToken: ct
            );
            return;
        }

        List<CurrencyRateRequest> rateRequests = transactions.Select(selector: t => new CurrencyRateRequest(
            From: t.Currency,
            To: budget.Currency,
            Date: DateOnly.FromDateTime(dateTime: t.OccurredAt.UtcDateTime)
        )).Distinct().ToList();

        Dictionary<CurrencyRateRequest, ConversionResult> rates = await currencyConversionService.GetConversionRatesBatchAsync(requests: rateRequests, ct: ct);

        decimal spent = transactions.Sum(selector: t =>
            t.Amount * rates[new CurrencyRateRequest(From: t.Currency, To: budget.Currency, Date: DateOnly.FromDateTime(dateTime: t.OccurredAt.UtcDateTime))].Rate
        );

        await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budgetId).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                 .SetProperty(propertyExpression: p => p.Spent, valueExpression: spent)
                 .SetProperty(propertyExpression: p => p.RowVersion, valueExpression: p => p.RowVersion + 1)
                .SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: dateProvider.UtcNow),
            cancellationToken: ct
        );
    }
}