using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;

namespace FinanceTracker.Core.Domains.Category;

public sealed class Category
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	public Guid? ParentId { get; private set; }
	public Name Name { get; private set; }
	public CategoryType Type { get; private set; }
	public bool IsArchived { get; private set; }
	public DateTime CreatedAt { get; private set; }

	private Category() { }

	public static Category Create(
		DateTime createdAt,
		Guid userId,
		Name name,
		CategoryType type,
		Guid? parentId)
	{
		return new Category()
		{
			Id = Guid.CreateVersion7(),
			UserId = userId,
			ParentId = parentId,
			Name = name,
			Type = type,
			IsArchived = false,
			CreatedAt = createdAt
		};
	}

	public static Category Reconstitute(
		Guid id,
		Guid userId,
		Guid? parentId,
		Name name,
		CategoryType type,
		bool isArchived,
		DateTime createdAt)
	{
		return new Category()
		{
			Id = id,
			UserId = userId,
			ParentId = parentId,
			Name = name,
			Type = type,
			IsArchived = isArchived,
			CreatedAt = createdAt
		};
	}

	public Result<Unit, DomainException> Rename(Name newName)
	{
		if (IsArchived)
			return Result<Unit, DomainException>.Failure(error: new ArchivingException(message: "It is forbidden to change the name of an archived category."));
 
		Name = newName;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Archive()
	{
		if (IsArchived)
			return Result<Unit, DomainException>.Failure(error: new ArchivingException(message: "The category has already been archived before."));
 
		IsArchived = true;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
 
	public Result<Unit, DomainException> Unarchive()
	{
		if (!IsArchived)
			return Result<Unit, DomainException>.Failure(error: new UnarchivingException(message: "The category is already active."));
 
		IsArchived = false;
		return Result<Unit, DomainException>.Success(value: Unit.Default);
	}
}