using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
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

        BudgetEntity? budget = await context.Budgets.AsNoTracking().FirstOrDefaultAsync(predicate: b =>
            b.UserId == userId &&
            b.CategoryId == categoryId &&
            b.From <= date &&
            b.To >= date,
            cancellationToken: ct
        );

        if (budget is null)
            return;

        decimal rate = await currencyConversionService.GetStableRateAsync(
            fromCurrency: currencyCode,
            toCurrency: budget.Currency,
            asOf: occurredAt,
            ct: ct
        );

        decimal additionSpent = delta * Money.ConvertedAmount(amount: amount, rate: rate);

        await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budget.Id).ExecuteUpdateAsync(
            setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: p => p.Spent, valueExpression: p => p.Spent + additionSpent)
                .SetProperty(propertyExpression: p => p.RowVersion, valueExpression: p => p.RowVersion + 1)
                .SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: dateProvider.UtcNow),
            cancellationToken: ct
        );
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

		List<CurrencyStableRateRequest> rateRequests = transactions.Select(selector: t => new CurrencyStableRateRequest(
			From: t.Currency,
			To: budget.Currency,
			AsOf: t.OccurredAt
		)).Distinct().ToList();

		Dictionary<CurrencyStableRateRequest, decimal> rates = await currencyConversionService.GetStableRatesBatchAsync(requests: rateRequests, ct: ct);

		decimal spent = transactions.Sum(selector: t => Money.ConvertedAmount(
			amount: t.Amount,
			rate: rates[new CurrencyStableRateRequest(From: t.Currency, To: budget.Currency, AsOf: t.OccurredAt)]
		));

		await context.BudgetProgresses.Where(predicate: p => p.BudgetId == budgetId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				 .SetProperty(propertyExpression: p => p.Spent, valueExpression: spent)
				 .SetProperty(propertyExpression: p => p.RowVersion, valueExpression: p => p.RowVersion + 1)
				.SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: dateProvider.UtcNow),
			cancellationToken: ct
		);
	}
}
