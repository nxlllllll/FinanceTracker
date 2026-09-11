using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Tests.Unit.Helpers;

public static class CategoryFactory
{
	public static Result<Category, DomainException> Create(
		Guid? userId = null,
		string name = "Еда",
		CategoryType type = CategoryType.Expense,
		Guid? parentId = null,
		bool archived = false,
		CategoryType? parentType = null)
	{
		Result<Category, DomainException> result = Category.Create(
			createdAt: FakeDateProvider.Default.UtcNow,
			userId: userId ?? Guid.CreateVersion7(),
			name: Name.Create(value: name).Value,
			type: type,
			parentId: parentId,
			parentType: parentType
		);

		if (result.IsFailure)
			return result;

		if (archived)
			result.Value!.Archive();

		return result;
	}

	public static CategoryReadModel CreateReadModel(
		Guid? userId = null,
		string name = "Еда",
		CategoryType type = CategoryType.Expense,
		Guid? parentId = null,
		bool archived = false)
	{
		return new CategoryReadModel(
			Id: Guid.CreateVersion7(),
			UserId: userId ?? Guid.CreateVersion7(),
			ParentId: parentId,
			Name: Name.Create(value: name).Value,
			Type: type,
			IsArchived: archived,
			CreatedAt: FakeDateProvider.Default.UtcNow
		);
	}
}
