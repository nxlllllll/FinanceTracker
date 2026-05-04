using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CategoryFactory
{
	public static Result<Category, DomainException> Create(
		Guid? userId = null,
		string name = "Еда",
		CategoryType type = CategoryType.Expense,
		Guid? parentId = null,
		bool archived = false)
	{
		Result<Category, DomainException> result = Category.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.NewGuid(),
			name: name,
			type: type,
			parentId: parentId
		);
		if (result.IsFailure)
			return Result<Category, DomainException>.Failure(error: result.Error!);
		
		Category category = result.Value!;

		if (archived)
			category.Archive();

		return Result<Category, DomainException>.Success(value: category);
	}
}