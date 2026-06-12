using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Context;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryRepository(FinanceTrackerContext context) : ICategoryRepository
{
	public async Task<Core.Domains.Category.Category?> GetByIdAsync(
		Guid categoryId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Categories.AsNoTracking().Where(predicate: c => c.Id == categoryId && c.UserId == userId)
			.Select(selector: c => Core.Domains.Category.Category.Reconstitute(
				id: c.Id,
				userId: c.UserId,
				parentId: c.ParentId,
				name: c.Name,
				type: c.Type,
				isArchived: c.IsArchived,
				rowVersion: c.RowVersion,
				createdAt: c.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}
}