using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryReadRepository(
	FinanceTrackerContext context
) : ICategoryReadRepository
{
	public async Task<Core.Domains.Category.Category?> GetByIdAsync(
		Guid categoryId,
		Guid userId,
		CancellationToken ct = default)
	{
		CategoryEntity? category = await context.Categories.AsNoTracking()
			.Where(predicate: category => category.Id == categoryId && category.UserId == userId)
			.FirstOrDefaultAsync(cancellationToken: ct);

		if (category is null)
			return null;

		return Core.Domains.Category.Category.Reconstitute(
			id: category.Id,
			userId: category.UserId,
			parentId: category.ParentId,
			name: Name.Reconstitute(value: category.Name),
			type: category.Type,
			isArchived: category.IsArchived,
			createdAt: category.CreatedAt
		);
	}
	
	public async Task<PagedResult<Core.Domains.Category.Category>> GetAllAsync(
		Guid userId,
		CategoryType? type = null,
		bool? isArchived = null,
		Guid? parentId = null,
		DateTimeOffset? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<CategoryEntity> query = context.Categories.AsNoTracking().Where(predicate: c => c.UserId == userId);

		if (type is not null)
			query = query.Where(predicate: c => c.Type == type);
 
		if (isArchived is not null)
			query = query.Where(predicate: c => c.IsArchived == isArchived);
 
		if (parentId is not null)
			query = query.Where(predicate: c => c.ParentId == parentId);
 
		if (cursorCreatedAt is not null && cursorId is not null)
			query = query.Where(predicate: c => c.CreatedAt < cursorCreatedAt || c.CreatedAt == cursorCreatedAt && c.Id < cursorId);
 
		List<Core.Domains.Category.Category> items = await query
			.OrderByDescending(keySelector: c => c.CreatedAt)
			.ThenByDescending(keySelector: c => c.Id)
			.Take(count: pageSize + 1)
			.Select(selector: c => Core.Domains.Category.Category.Reconstitute(
				id: c.Id,
				userId: c.UserId,
				parentId: c.ParentId,
				name: c.Name,
				type: c.Type,
				isArchived: c.IsArchived,
				createdAt: c.CreatedAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(items.Count - 1);

		Core.Domains.Category.Category? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<Core.Domains.Category.Category>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.CreatedAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}
}
