using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.CategoryTotals;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.CategoryTotal;

public sealed class CategoryTotalReadRepository(
	FinanceTrackerContext context
) : ICategoryTotalReadRepository
{
	public async Task<CategoryTotalDto?> GetByCategoryAsync(
		Guid userId,
		Guid categoryId,
		DateOnly period,
		CancellationToken ct = default)
	{
		return await context.CategoryTotals.AsNoTracking()
			.Where(predicate: total => total.UserId == userId && total.CategoryId == categoryId && total.Period == period)
			.Select(selector: total => new CategoryTotalDto(
				CategoryId: total.CategoryId,
				Period: total.Period,
				Total: total.Total,
				Count: total.TransactionCount,
				UpdatedAt: total.UpdatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<IReadOnlyList<CategoryTotalDto>> GetAllByPeriodAsync(
		Guid userId,
		DateOnly period,
		CancellationToken ct = default)
	{
		return await context.CategoryTotals.AsNoTracking().Where(predicate: total => total.UserId == userId && total.Period == period)
			.Select(selector: total => new CategoryTotalDto(
				CategoryId: total.CategoryId,
				Period: total.Period,
				Total: total.Total,
				Count: total.TransactionCount,
				UpdatedAt: total.UpdatedAt
			)).ToListAsync(cancellationToken: ct);
	}

	public async Task<(decimal Income, decimal Expense)> GetIncomeExpenseSummaryAsync(
		Guid userId,
		DateOnly period,
		CancellationToken ct = default)
	{
		var summary = context.CategoryTotals.AsNoTracking()
			.Where(predicate: total => total.UserId == userId && total.Period == period)
			.Join(
				inner: context.Categories,
				outerKeySelector: total => total.CategoryId,
				innerKeySelector: category => category.Id,
				resultSelector: (total, category) => new { total.Total, category.Type }
			).GroupBy(keySelector: x => x.Type)
			.Select(selector: g => new { Type = g.Key, Sum = g.Sum(x => x.Total) });

		decimal income = summary.FirstOrDefault(predicate: x => x.Type == Core.Domains.Category.CategoryType.Income)?.Sum ?? 0;
		decimal expense = summary.FirstOrDefault(predicate: x => x.Type == Core.Domains.Category.CategoryType.Expense)?.Sum ?? 0;
 
		return (income, expense);
	}
}