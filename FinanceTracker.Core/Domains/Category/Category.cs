using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Core.Domains.Category;

public sealed class Category
{
	public Guid Id { get; private set; }
	public Guid UserId { get; private set; }
	public Guid? ParentId { get; private set; }
	public string Name { get; private set; } = String.Empty;
	public CategoryType Type { get; private set; }
	public bool IsArchived { get; private set; }
	public DateTime CreatedAt { get; private set; }

	private Category() { }

	public static Category Create(
		DateTime createdAt,
		Guid userId,
		string name,
		CategoryType type,
		Guid? parentId)
	{
		if (String.IsNullOrWhiteSpace(value: name))
			throw new EmptyNameException(message: "The category name cannot be empty.");

		return new Category()
		{
			Id = Guid.NewGuid(),
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
		string name,
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

	public void Rename(string newName)
	{
		if (String.IsNullOrWhiteSpace(value: newName))
			throw new EmptyNameException(message: "The category name cannot be empty.");

		if (IsArchived)
			throw new ArchivingException(message: "It is forbidden to change the name of an archived category.");

		if (newName.Equals(value: Name))
			return;

		Name = newName;
	}

	public void Archive()
	{
		if (IsArchived)
			throw new ArchivingException(message: "The category has already been archived before.");

		IsArchived = true;
	}

	public void Unarchive()
	{
		if (!IsArchived)
			throw new UnarchivingException(message: "The category is already active.");

		IsArchived = false;
	}
}