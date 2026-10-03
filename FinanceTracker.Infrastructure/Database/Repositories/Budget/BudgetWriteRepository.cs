using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Services.DateProvider;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetWriteRepository(
	FinanceTrackerContext context,
	IDateProvider dateProvider,
	BudgetThresholdAlerts budgetThresholdAlerts
) : IBudgetWriteRepository
{
	private sealed record LimitChange(decimal LimitBefore, decimal LimitAfter);

	private sealed record Progress(decimal Spent, int RowVersion);

	public async Task CreateAsync(
		Core.Domains.Budget.Budget budget,
		CancellationToken ct = default)
	{
		await context.Budgets.AddAsync(entity: new BudgetEntity()
		{
			Id = budget.Id,
			UserId = budget.UserId,
			CategoryId = budget.CategoryId,
			Amount = budget.Amount.Amount,
			Currency = budget.Amount.Currency,
			From = budget.From,
			To = budget.To,
			IsActive = true,
			RowVersion = 0,
			CreatedAt = dateProvider.UtcNow
		}, cancellationToken: ct);

		await context.BudgetProgresses.AddAsync(entity: new BudgetProgressEntity()
		{
			BudgetId = budget.Id,
			Spent = 0,
			UpdatedAt = dateProvider.UtcNow
		}, cancellationToken: ct);
	}

	public async Task ChangeAmountAsync(
		Guid budgetId,
		decimal amount,
		int expectedVersion,
		CancellationToken ct = default)
	{
		List<LimitChange> changes = await context.Database.SqlQuery<LimitChange>(sql: $"""
			UPDATE budgets
			SET amount = {amount}, row_version = {expectedVersion + 1}
			WHERE id = {budgetId} AND row_version = {expectedVersion}
			RETURNING old.amount AS "LimitBefore", new.amount AS "LimitAfter"
		""").ToListAsync(cancellationToken: ct);

		LimitChange? change = changes.SingleOrDefault();
		if (change is null)
			throw new ConcurrencyConflictException(message: $"Budget {budgetId} was modified by another request.", id: budgetId);

		Progress progress = await context.Database.SqlQuery<Progress>(sql: $"""
			SELECT spent AS "Spent", row_version AS "RowVersion"
			FROM rm_budget_progress
			WHERE budget_id = {budgetId}
			FOR UPDATE
		""").SingleAsync(cancellationToken: ct);

		BudgetEntity budget = await context.Budgets.AsNoTracking().SingleAsync(predicate: b => b.Id == budgetId, cancellationToken: ct);

		budgetThresholdAlerts.StageIfCrossed(
			budget: budget,
			spentBefore: progress.Spent,
			limitBefore: change.LimitBefore,
			spentAfter: progress.Spent,
			version: progress.RowVersion
		);
	}

	public async Task ChangePeriodAsync(
		Guid budgetId,
		DateOnly from,
		DateOnly to,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Budgets.Where(predicate: b => b.Id == budgetId && b.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: b => b.From, valueExpression: from)
				.SetProperty(propertyExpression: b => b.To, valueExpression: to)
				.SetProperty(propertyExpression: b => b.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Budget {budgetId} was modified by another request.", id: budgetId);
	}

	public async Task ActivateAsync(
		Guid budgetId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Budgets.Where(predicate: b => b.Id == budgetId && b.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: b => b.IsActive, valueExpression: true)
				.SetProperty(propertyExpression: b => b.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Budget {budgetId} was modified by another request.", id: budgetId);
	}

	public async Task DeactivateAsync(
		Guid budgetId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Budgets.Where(predicate: b => b.Id == budgetId && b.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: b => b.IsActive, valueExpression: false)
				.SetProperty(propertyExpression: b => b.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Budget {budgetId} was modified by another request.", id: budgetId);
	}

	public async Task DeactivateByCategoryIdAsync(
		Guid categoryId,
		CancellationToken ct = default)
	{
		await context.Budgets.Where(predicate: b => b.CategoryId == categoryId).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: b => b.IsActive, valueExpression: false)
				.SetProperty(propertyExpression: b => b.RowVersion, valueExpression: b => b.RowVersion + 1),
			cancellationToken: ct
		);
	}
}
