using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Budget;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Budget;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Budget;

public sealed class BudgetReadRepository(
	FinanceTrackerContext context
) : IBudgetReadRepository
{
	public async Task<BudgetReadModel?> GetByIdAsync(
		Guid budgetId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Budgets.AsNoTracking().Where(predicate: b => b.Id == budgetId && b.UserId == userId)
			.Select(selector: b => new BudgetReadModel(
				Id: b.Id,
				UserId: b.UserId,
				CategoryId: b.CategoryId,
				Amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
				From: b.From,
				To: b.To,
				IsActive: b.IsActive,
				CreatedAt: b.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<BudgetReadModel?> GetActiveByCategoryAsync(
		Guid userId,
		Guid categoryId,
		DateOnly date,
		CancellationToken ct = default)
	{
		return await context.Budgets.AsNoTracking().Where(predicate: b => b.UserId == userId && b.CategoryId == categoryId && b.IsActive && b.From <= date && b.To >= date)
			.Select(selector: b => new BudgetReadModel(
				Id: b.Id,
				UserId: b.UserId,
				CategoryId: b.CategoryId,
				Amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
				From: b.From,
				To: b.To,
				IsActive: b.IsActive,
				CreatedAt: b.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<bool> HasOverlappingAsync(
		Guid userId,
		Guid categoryId,
		DateOnly from,
		DateOnly to,
		Guid? excludeBudgetId = null,
		CancellationToken ct = default)
	{
		IQueryable<BudgetEntity> query = context.Budgets.AsNoTracking()
			.Where(predicate: b => b.UserId == userId && b.CategoryId == categoryId && b.IsActive && b.From < to && b.To > from);

		if (excludeBudgetId is not null)
			query = query.Where(predicate: b => b.Id != excludeBudgetId);

		return await query.AnyAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<BudgetReadModel>> GetAllAsync(
		Guid userId,
		DateTimeOffset? cursorCreatedAt = null,
		bool? isActive = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<BudgetEntity> query = context.Budgets.AsNoTracking().Where(predicate: b => b.UserId == userId);
		if (isActive is not null)
			query = query.Where(predicate: b => b.IsActive == isActive);

		if (cursorCreatedAt is not null && cursorId is not null)
			query = query.Where(predicate: b => b.CreatedAt < cursorCreatedAt || b.CreatedAt == cursorCreatedAt && b.Id < cursorId);

		List<BudgetReadModel> items = await query
			.OrderByDescending(keySelector: b => b.CreatedAt)
			.ThenByDescending(keySelector: b => b.Id)
			.Take(count: pageSize + 1)
			.Select(selector: b => new BudgetReadModel(
				Id: b.Id,
				UserId: b.UserId,
				CategoryId: b.CategoryId,
				Amount: Money.Reconstitute(amount: b.Amount, currency: b.Currency),
				From: b.From,
				To: b.To,
				IsActive: b.IsActive,
				CreatedAt: b.CreatedAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(index: items.Count - 1);

		BudgetReadModel? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<BudgetReadModel>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.CreatedAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}
}
