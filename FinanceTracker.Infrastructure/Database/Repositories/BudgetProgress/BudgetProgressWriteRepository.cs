using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Services.CurrencyConversion;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.BudgetProgress;

public sealed class BudgetProgressWriteRepository(
    FinanceTrackerContext context,
    ICurrencyConversionService currencyConversionService
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
                        valueExpression: DateTime.UtcNow
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
}