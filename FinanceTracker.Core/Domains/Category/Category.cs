using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Category;

/// <summary>
/// Represents a transaction category (e.g. Food, Transport).
/// Categories can be nested via <see cref="ParentId"/> for sub-category grouping.
/// </summary>
public sealed class Category : IHasId
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	/// <summary>Optional parent category ID for hierarchical grouping. <c>null</c> for root categories.</summary>
	public Guid? ParentId { get; private set; }
	public Name Name { get; private set; }
	public CategoryType Type { get; private set; }
	public bool IsArchived { get; private set; }
	public int RowVersion { get; private set; }
	public DateTimeOffset CreatedAt { get; private set; }

	private Category() { }

	/// <summary>
	/// <paramref name="parentType"/> is the type of the parent named by <paramref name="parentId"/>,
	/// or <c>null</c> for a root category. Depth is checked by the caller, the only side that can see
	/// the rest of the tree.
	/// </summary>
	public static Result<Category, DomainException> Create(
		DateTimeOffset createdAt,
		Guid userId,
		Name name,
		CategoryType type,
		Guid? parentId,
		CategoryType? parentType)
	{
		if (parentType is not null && parentType != type)
			return Result<Category, DomainException>.Failure(error: new CategoryTypeMismatchException(message: "A category must have the same type as its parent."));

		return Result<Category, DomainException>.Success(value: new Category()
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			ParentId = parentId,
			Name = name,
			Type = type,
			IsArchived = false,
			RowVersion = 0,
			CreatedAt = createdAt
		});
	}

	public static Category Reconstitute(
		Guid id,
		Guid userId,
		Guid? parentId,
		Name name,
		CategoryType type,
		bool isArchived,
		int rowVersion,
		DateTimeOffset createdAt)
	{
		return new Category()
		{
			Id = id,
			UserId = userId,
			ParentId = parentId,
			Name = name,
			Type = type,
			IsArchived = isArchived,
			RowVersion = rowVersion,
			CreatedAt = createdAt
		};
	}

	public Result<bool, DomainException> Rename(Name newName)
	{
		if (IsArchived)
			return Result<bool, DomainException>.Failure(error: new ArchivedOperationException(message: "It is forbidden to change the name of an archived category."));

		if (Name == newName)
			return Result<bool, DomainException>.Success(value: false);

		Name = newName;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> ChangeParent(Guid? newParentId, CategoryType? newParentType)
	{
		if (IsArchived)
			return Result<bool, DomainException>.Failure(error: new ArchivedOperationException(message: "It is forbidden to move an archived category."));

		if (newParentId == Id)
			return Result<bool, DomainException>.Failure(error: new CategoryCycleException(message: "A category cannot be its own parent."));

		if (newParentType is not null && newParentType != Type)
			return Result<bool, DomainException>.Failure(error: new CategoryTypeMismatchException(message: "A category must have the same type as its parent."));

		if (ParentId == newParentId)
			return Result<bool, DomainException>.Success(value: false);

		ParentId = newParentId;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> Archive()
	{
		if (IsArchived)
			return Result<bool, DomainException>.Success(value: false);

		IsArchived = true;
		return Result<bool, DomainException>.Success(value: true);
	}

	public Result<bool, DomainException> Unarchive()
	{
		if (!IsArchived)
			return Result<bool, DomainException>.Success(value: false);

		IsArchived = false;
		return Result<bool, DomainException>.Success(value: true);
	}
}
