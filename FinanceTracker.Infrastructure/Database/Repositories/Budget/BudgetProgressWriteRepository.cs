using FinanceTracker.Contracts.Events.Budget;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Services.Currency;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Configurations.Options;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using FinanceTracker.Infrastructure.Database.Context.Outbox;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetProgressWriteRepository(
	FinanceTrackerContext context,
	ICurrencyConversionService currencyConversionService,
	IDateProvider dateProvider,
	IOptionsMonitor<BudgetAlertOptions> budgetAlertOptions,
	IIntegrationEventTypeResolver integrationEventTypeResolver,
	ICorrelationContext correlationContext
) : IBudgetProgressWriteRepository
{
	private sealed record SpentChange(decimal SpentBefore, decimal SpentAfter, int Version);

	private async Task<SpentChange?> IncreaseSpentAsync(Guid budgetId, decimal addition, CancellationToken ct)
	{
		List<SpentChange> rows = await context.Database.SqlQuery<SpentChange>(sql: $"""
			UPDATE rm_budget_progress
			SET spent = spent + {addition}, row_version = row_version + 1, updated_at = {dateProvider.UtcNow}
			WHERE budget_id = {budgetId}
			RETURNING old.spent AS "SpentBefore", new.spent AS "SpentAfter", new.row_version AS "Version"
		""").ToListAsync(cancellationToken: ct);

		return rows.SingleOrDefault();
	}

	private async Task<SpentChange?> SetSpentAsync(Guid budgetId, decimal spent, CancellationToken ct)
	{
		List<SpentChange> rows = await context.Database.SqlQuery<SpentChange>(sql: $"""
			UPDATE rm_budget_progress
			SET spent = {spent}, row_version = row_version + 1, updated_at = {dateProvider.UtcNow}
			WHERE budget_id = {budgetId}
			RETURNING old.spent AS "SpentBefore", new.spent AS "SpentAfter", new.row_version AS "Version"
		""").ToListAsync(cancellationToken: ct);

		return rows.SingleOrDefault();
	}

	private async Task StageCrossedThresholdAsync(
		Guid budgetId, 
		SpentChange change,
		CancellationToken ct)
	{
		BudgetEntity budget = await context.Budgets.AsNoTracking().SingleAsync(predicate: b => b.Id == budgetId, cancellationToken: ct);
		if (!budget.IsActive)
			return;

		int? threshold = BudgetThresholds.GetHighestCrossed(
			spentBefore: change.SpentBefore,
			limitBefore: budget.Amount,
			spentAfter: change.SpentAfter,
			limitAfter: budget.Amount,
			thresholds: budgetAlertOptions.CurrentValue.Thresholds
		);

		if (threshold is null)
			return;

		BudgetThresholdReachedEvent integrationEvent = new BudgetThresholdReachedEvent(
			EventId: Guid.CreateVersion7(),
			BudgetId: budget.Id,
			UserId: budget.UserId,
			CategoryId: budget.CategoryId,
			Threshold: threshold.Value,
			Spent: change.SpentAfter,
			Limit: budget.Amount,
			Currency: budget.Currency,
			Version: change.Version,
			OccurredAt: dateProvider.UtcNow
		);

		context.OutboxMessages.Add(entity: OutboxMessageFactory.CreateMessage(
			aggregateId: budget.Id,
			aggregateType: AggregateTypeNames.Budget,
			correlationId: correlationContext.CorrelationId,
			envelopes: [OutboxMessageFactory.CreateEnvelope(integrationEvent: integrationEvent, integrationEventTypeResolver: integrationEventTypeResolver)],
			now: dateProvider.UtcNow
		));
	}

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
			decimal rate = await currencyConversionService.GetStableRateAsync(
				fromCurrency: currencyCode,
				toCurrency: budget.Currency,
				asOf: occurredAt,
				ct: ct
			);

			decimal additionSpent = delta * Money.ConvertedAmount(amount: amount, rate: rate);

			SpentChange? change = await IncreaseSpentAsync(budgetId: budget.Id, addition: additionSpent, ct: ct);
			if (change is not null)
				await StageCrossedThresholdAsync(budgetId: budget.Id, change: change, ct: ct);
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

		decimal spent = 0m;

		if (transactions.Count > 0)
		{
			List<CurrencyStableRateRequest> rateRequests = transactions.Select(selector: t => new CurrencyStableRateRequest(
				From: t.Currency,
				To: budget.Currency,
				AsOf: t.OccurredAt
			)).Distinct().ToList();

			Dictionary<CurrencyStableRateRequest, decimal> rates = await currencyConversionService.GetStableRatesBatchAsync(requests: rateRequests, ct: ct);

			spent = transactions.Sum(selector: t => Money.ConvertedAmount(
				amount: t.Amount,
				rate: rates[new CurrencyStableRateRequest(From: t.Currency, To: budget.Currency, AsOf: t.OccurredAt)]
			));
		}

		SpentChange? change = await SetSpentAsync(budgetId: budgetId, spent: spent, ct: ct);
		if (change is not null)
			await StageCrossedThresholdAsync(budgetId: budgetId, change: change, ct: ct);
	}
}
