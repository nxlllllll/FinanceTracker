using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
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
	
	public async Task<IReadOnlyList<Core.Domains.Category.Category>> GetAllAsync(
		Guid userId,
		CategoryType? type = null,
		bool? isArchived = null,
		Guid? parentId = null,
		DateTime? cursorCreatedAt = null,
		Guid? cursorId = null,
		int pageSize = 20,
		CancellationToken ct = default)
	{
		IQueryable<CategoryEntity> categories = context.Categories.AsNoTracking().Where(predicate: c => c.UserId == userId);
 
		if (type is not null)
			categories = categories.Where(predicate: c => c.Type == type);
 
		if (isArchived is not null)
			categories = categories.Where(predicate: c => c.IsArchived == isArchived);
 
		if (parentId is not null)
			categories = categories.Where(predicate: c => c.ParentId == parentId);
 
		if (cursorCreatedAt is not null && cursorId is not null)
			categories = categories.Where(predicate: c => c.CreatedAt < cursorCreatedAt || c.CreatedAt == cursorCreatedAt && c.Id < cursorId);
 
		return await categories
			.OrderByDescending(keySelector: c => c.CreatedAt)
			.ThenByDescending(keySelector: c => c.Id)
			.Take(count: pageSize)
			.Select(selector: c => Core.Domains.Category.Category.Reconstitute(
				id: c.Id,
				userId: c.UserId,
				parentId: c.ParentId,
				name: c.Name,
				type: c.Type,
				isArchived: c.IsArchived,
				createdAt: c.CreatedAt
			)).ToListAsync(cancellationToken: ct);
	}
}