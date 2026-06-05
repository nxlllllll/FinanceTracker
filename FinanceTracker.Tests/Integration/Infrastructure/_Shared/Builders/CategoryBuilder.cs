using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Category;

namespace FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

public class CategoryBuilder(FinanceTrackerContext context)
{
	public async Task<Guid> CreateAsync(
		Guid userId,
		string name = "Еда",
		CategoryType type = CategoryType.Expense)
	{
		Guid categoryId = Guid.CreateVersion7();
		await context.Categories.AddAsync(new CategoryEntity
		{
			Id = categoryId,
			UserId = userId,
			ParentId = null,
			Name = Name.Create(value: name).Value,
			Type = type,
			IsArchived = false,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await context.SaveChangesAsync();
		return categoryId;
	}
}