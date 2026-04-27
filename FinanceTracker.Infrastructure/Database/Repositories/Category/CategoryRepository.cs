using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryRepository(
	FinanceTrackerContext context
) : ICategoryRepository
{
	private async Task ChangeCategoryProperty(
		Guid categoryId,
		Action<UpdateSettersBuilder<CategoryEntity>> changePropertyAction,
		CancellationToken ct = default)
	{
		await context.Categories.Where(predicate: category => category.Id == categoryId).ExecuteUpdateAsync(
			setPropertyCalls: changePropertyAction,
			cancellationToken: ct
		);
	}

	public async Task<Core.Domains.Category.Category?> GetByIdAsync(
		Guid categoryId,
		CancellationToken ct = default)
	{
		CategoryEntity? category = await context.Categories.AsNoTracking()
			.Where(predicate: category => category.Id == categoryId)
			.FirstOrDefaultAsync(cancellationToken: ct);

		if (category is null)
			return null;

		return Core.Domains.Category.Category.Reconstitute(
			id: category.Id,
			userId: category.UserId,
			parentId: category.ParentId,
			name: category.Name,
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
		CancellationToken ct = default)
	{
		IQueryable<CategoryEntity> categories = context.Categories.AsNoTracking()
			.Where(predicate: c => c.UserId == userId);

		if (type is not null)
			categories = categories.Where(predicate: c => c.Type == type);

		if (isArchived is not null)
			categories = categories.Where(predicate: c => c.IsArchived == isArchived);

		if (parentId is not null)
			categories = categories.Where(predicate: c => c.ParentId == parentId);
    
		return await categories.Select(selector: c => Core.Domains.Category.Category.Reconstitute(
			id: c.Id,
			userId: c.UserId,
			parentId: c.ParentId,
			name: c.Name,
			type: c.Type,
			isArchived: c.IsArchived,
			createdAt: c.CreatedAt
		)).ToListAsync(cancellationToken: ct);
	}

	public async Task CreateAsync(
		Core.Domains.Category.Category category,
		CancellationToken ct = default)
	{
		await context.Categories.AddAsync(entity: new CategoryEntity()
		{
			Id = category.Id,
			UserId = category.UserId,
			ParentId = category.ParentId,
			Name = category.Name,
			Type = category.Type,
			IsArchived = false,
			CreatedAt = category.CreatedAt
		}, cancellationToken: ct);

		await context.SaveChangesAsync(cancellationToken: ct);
	}

	public async Task RenameAsync(
		Guid categoryId,
		string newName, CancellationToken ct = default)
	{
		await ChangeCategoryProperty(
			categoryId: categoryId,
			changePropertyAction: builder =>
				builder.SetProperty(propertyExpression: category => category.Name, valueExpression: newName),
			ct: ct
		);
	}

	public async Task ArchiveAsync(
		Guid categoryId,
		CancellationToken ct = default)
	{
		await ChangeCategoryProperty(
			categoryId: categoryId,
			builder => builder.SetProperty(propertyExpression: category => category.IsArchived, valueExpression: true),
			ct: ct
		);
	}

	public async Task UnarchiveAsync(
		Guid categoryId,
		CancellationToken ct = default)
	{
		await ChangeCategoryProperty(
			categoryId: categoryId,
			changePropertyAction: builder =>
				builder.SetProperty(propertyExpression: category => category.IsArchived, valueExpression: false),
			ct: ct
		);
	}
}