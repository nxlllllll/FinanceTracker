using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryWriteRepository(
	FinanceTrackerContext context
) : ICategoryWriteRepository
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