using FinanceTracker.Core.Exceptions.DomainExceptions.Platform.Concurrency;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Infrastructure.Database.Repositories.Category;

public sealed class CategoryWriteRepository(FinanceTrackerContext context) : ICategoryWriteRepository
{
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
			RowVersion = 0,
			CreatedAt = category.CreatedAt
		}, cancellationToken: ct);
	}

	public async Task ChangeParentAsync(
		Guid categoryId,
		Guid? newParentId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Categories.Where(predicate: c => c.Id == categoryId && c.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: c => c.ParentId, valueExpression: newParentId)
				.SetProperty(propertyExpression: c => c.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Category {categoryId} was modified by another request.", id: categoryId);
	}

	public async Task RenameAsync(
		Guid categoryId,
		Name newName,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Categories.Where(predicate: c => c.Id == categoryId && c.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: c => c.Name, valueExpression: newName)
				.SetProperty(propertyExpression: c => c.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Category {categoryId} was modified by another request.", id: categoryId);
	}

	public async Task ArchiveAsync(
		Guid categoryId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Categories.Where(predicate: c => c.Id == categoryId && c.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: c => c.IsArchived, valueExpression: true)
				.SetProperty(propertyExpression: c => c.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Category {categoryId} was modified by another request.", id: categoryId);
	}

	public async Task UnarchiveAsync(
		Guid categoryId,
		int expectedVersion,
		CancellationToken ct = default)
	{
		int affected = await context.Categories.Where(predicate: c => c.Id == categoryId && c.RowVersion == expectedVersion).ExecuteUpdateAsync(
			setPropertyCalls: builder => builder
				.SetProperty(propertyExpression: c => c.IsArchived, valueExpression: false)
				.SetProperty(propertyExpression: c => c.RowVersion, valueExpression: expectedVersion + 1),
			cancellationToken: ct
		);

		if (affected == 0)
			throw new ConcurrencyConflictException(message: $"Category {categoryId} was modified by another request.", id: categoryId);
	}
}
