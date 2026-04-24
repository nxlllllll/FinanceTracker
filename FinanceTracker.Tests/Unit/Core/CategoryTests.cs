using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class CategoryTests
{
	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.NewGuid();
		Category category = CategoryFactory.Create(userId: userId);

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
		Category category = CategoryFactory.Create(parentId: parentId);

		await Assert.That(value: category.ParentId).IsEqualTo(expected: parentId);
	}

	[Test]
	public async Task Create_WithEmptyName_ShouldThrowEmptyNameException()
		=> await Assert.That(func: () => CategoryFactory.Create(name: String.Empty)).Throws<EmptyNameException>();

	[Test]
	public async Task Rename_WithValidName_ShouldChangeName()
	{
		Category category = CategoryFactory.Create();

		category.Rename(newName: "Продукты");

		await Assert.That(value: category.Name).IsEqualTo(expected: "Продукты");
	}

	[Test]
	public async Task Rename_WithSameName_ShouldNotChangeName()
	{
		Category category = CategoryFactory.Create();

		category.Rename(newName: "Еда");

		await Assert.That(value: category.Name).IsEqualTo(expected: "Еда");
	}

	[Test]
	public async Task Rename_WithEmptyName_ShouldThrowEmptyNameException()
	{
		Category category = CategoryFactory.Create();

		await Assert.That(action: () => category.Rename(newName: String.Empty)).Throws<EmptyNameException>();
	}

	[Test]
	public async Task Rename_WhenArchived_ShouldThrowArchivingException()
	{
		Category category = CategoryFactory.Create();

		category.Archive();

		await Assert.That(action: () => category.Rename(newName: "Продукты")).Throws<ArchivingException>();
	}

	[Test]
	public async Task Archive_WithActiveCategory_ShouldSetIsArchivedTrue()
	{
		Category category = CategoryFactory.Create();

		category.Archive();

		await Assert.That(value: category.IsArchived).IsTrue();
	}

	[Test]
	public async Task Archive_WhenAlreadyArchived_ShouldThrowArchivingException()
	{
		Category category = CategoryFactory.Create();

		category.Archive();

		await Assert.That(action: category.Archive).Throws<ArchivingException>();
	}

	[Test]
	public async Task Unarchive_WhenArchived_ShouldSetIsArchivedFalse()
	{
		Category category = CategoryFactory.Create();

		category.Archive();
		category.Unarchive();

		await Assert.That(value: category.IsArchived).IsFalse();
	}

	[Test]
	public async Task Unarchive_WhenNotArchived_ShouldThrowUnarchivingException()
	{
		Category category = CategoryFactory.Create();

		await Assert.That(action: category.Unarchive).Throws<UnarchivingException>();
	}
}