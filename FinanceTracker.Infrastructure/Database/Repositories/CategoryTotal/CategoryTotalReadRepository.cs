using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.CategoryTotals;
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
}