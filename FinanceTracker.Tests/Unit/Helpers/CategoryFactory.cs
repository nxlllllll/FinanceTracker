using FinanceTracker.Core.Domains.Category;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CategoryFactory
{
	public static Category Create(
		Guid? userId = null,
		string name = "Еда",
		CategoryType type = CategoryType.Expense,
		Guid? parentId = null,
		bool archived = false)
	{
		Category category = Category.Create(
			userId: userId ?? Guid.NewGuid(),
			name: name,
			type: type,
			parentId: parentId
		);

		if (archived)
			category.Archive();

		return category;
	}
}