using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryReadRepository(FinanceTrackerContext context) : ICategoryReadRepository
{
	public async Task<CategoryReadModel?> GetByIdAsync(
		Guid categoryId,
		Guid userId,
		CancellationToken ct = default)
	{
		return await context.Categories.AsNoTracking().Where(predicate: c => c.Id == categoryId && c.UserId == userId)
			.Select(selector: c => new CategoryReadModel(
				Id: c.Id,
				UserId: c.UserId,
				ParentId: c.ParentId,
				Name: c.Name,
				Type: c.Type,
				IsArchived: c.IsArchived,
				CreatedAt: c.CreatedAt
			)).FirstOrDefaultAsync(cancellationToken: ct);
	}

	public async Task<PagedResult<CategoryReadModel>> GetAllAsync(
		Guid userId,
		Core.Domains.Category.CategoryType? type = null,
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

		List<CategoryReadModel> items = await query
			.OrderByDescending(keySelector: c => c.CreatedAt)
			.ThenByDescending(keySelector: c => c.Id)
			.Take(count: pageSize + 1)
			.Select(selector: c => new CategoryReadModel(
				Id: c.Id,
				UserId: c.UserId,
				ParentId: c.ParentId,
				Name: c.Name,
				Type: c.Type,
				IsArchived: c.IsArchived,
				CreatedAt: c.CreatedAt
			)).ToListAsync(cancellationToken: ct);

		bool hasNextPage = items.Count > pageSize;
		if (hasNextPage)
			items.RemoveAt(items.Count - 1);

		CategoryReadModel? last = items.Count > 0 ? items[^1] : null;

		return new PagedResult<CategoryReadModel>(
			Items: items.AsReadOnly(),
			HasNextPage: hasNextPage,
			NextCursorDate: hasNextPage ? last?.CreatedAt : null,
			NextCursorId: hasNextPage ? last?.Id : null
		);
	}

	public async Task<bool> ExistsAsync(
		Guid categoryId,
		Guid userId,
		CancellationToken ct = default
	) => await context.Categories.AsNoTracking().AnyAsync(predicate: c => c.Id == categoryId && c.UserId == userId, cancellationToken: ct);
}
