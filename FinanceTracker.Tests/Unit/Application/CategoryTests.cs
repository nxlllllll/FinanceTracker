using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Tests.Unit.Application;

public sealed class CategoryTests
{
	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.NewGuid();
		Category category = Category.Create(
			userId: userId,
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);

		await Assert.That(value: category.Id).IsNotDefault();
		await Assert.That(value: category.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: category.Name).IsEqualTo(expected: "Еда");
		await Assert.That(value: category.Type).IsEqualTo(expected: CategoryType.Expense);
		await Assert.That(value: category.ParentId).IsNull();
		await Assert.That(value: category.IsArchived).IsFalse();
		await Assert.That(value: category.CreatedAt).IsNotDefault();
	}

	[Test]
	public async Task Create_WithParentId_ShouldSetParentId()
	{
		Guid parentId = Guid.NewGuid();
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Фастфуд",
			type: CategoryType.Expense,
			parentId: parentId
		);

		await Assert.That(value: category.ParentId).IsEqualTo(expected: parentId);
	}

	[Test]
	public async Task Create_WithEmptyName_ShouldThrowEmptyNameException()
	{
		await Assert.That(func: () => Category.Create(
			userId: Guid.NewGuid(),
			name: String.Empty,
			type: CategoryType.Expense,
			parentId: null
		)).Throws<EmptyNameException>();
	}

	[Test]
	public async Task Rename_WithValidName_ShouldChangeName()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);

		category.Rename(newName: "Продукты");

		await Assert.That(value: category.Name).IsEqualTo(expected: "Продукты");
	}

	[Test]
	public async Task Rename_WithSameName_ShouldNotChangeName()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);

		category.Rename(newName: "Еда");

		await Assert.That(value: category.Name).IsEqualTo(expected: "Еда");
	}

	[Test]
	public async Task Rename_WithEmptyName_ShouldThrowEmptyNameException()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);

		await Assert.That(action: () => category.Rename(newName: String.Empty)).Throws<EmptyNameException>();
	}

	[Test]
	public async Task Rename_WhenArchived_ShouldThrowArchivingException()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);
		category.Archive();

		await Assert.That(action: () => category.Rename(newName: "Продукты")).Throws<ArchivingException>();
	}

	[Test]
	public async Task Archive_WithActiveCategory_ShouldSetIsArchivedTrue()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);

		category.Archive();

		await Assert.That(value: category.IsArchived).IsTrue();
	}

	[Test]
	public async Task Archive_WhenAlreadyArchived_ShouldThrowArchivingException()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);
		category.Archive();

		await Assert.That(action: category.Archive).Throws<ArchivingException>();
	}

	[Test]
	public async Task Unarchive_WhenArchived_ShouldSetIsArchivedFalse()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);
		category.Archive();
		category.Unarchive();

		await Assert.That(value: category.IsArchived).IsFalse();
	}

	[Test]
	public async Task Unarchive_WhenNotArchived_ShouldThrowUnarchivingException()
	{
		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);

		await Assert.That(action: category.Unarchive).Throws<UnarchivingException>();
	}
}