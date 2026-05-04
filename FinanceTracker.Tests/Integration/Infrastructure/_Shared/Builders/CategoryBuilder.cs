using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Entities;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class CategoryBuilder(FinanceTrackerContext context)
{
	public async Task<Guid> CreateAsync(
		Guid userId,
		string name = "Еда",
		CategoryType type = CategoryType.Expense)
	{
		Guid categoryId = Guid.NewGuid();
		await context.Categories.AddAsync(new CategoryEntity
		{
			Id = categoryId,
			UserId = userId,
			ParentId = null,
			Name = name,
			Type = type,
			IsArchived = false,
			CreatedAt = DateTime.UtcNow
		});
		await context.SaveChangesAsync();
		return categoryId;
	}
}