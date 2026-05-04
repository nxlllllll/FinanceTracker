using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class CategoryTests
{
	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.NewGuid();
		Category category = CategoryFactory.Create(userId: userId).Value!;

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
		Category category = CategoryFactory.Create(parentId: parentId).Value!;

		await Assert.That(value: category.ParentId).IsEqualTo(expected: parentId);
	}

	[Test]
	public async Task Create_WithEmptyName_ShouldThrowEmptyNameException()
	{
		Result<Category, DomainException> result = CategoryFactory.Create(name: String.Empty);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NameException>();
	}

	[Test]
	public async Task Rename_WithValidName_ShouldChangeName()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Rename(newName: "Продукты");

		await Assert.That(value: category.Name).IsEqualTo(expected: "Продукты");
	}

	[Test]
	public async Task Rename_WithSameName_ShouldNotChangeName()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Rename(newName: "Еда");

		await Assert.That(value: category.Name).IsEqualTo(expected: "Еда");
	}

	[Test]
	public async Task Rename_WithEmptyName_ShouldThrowEmptyNameException()
	{
		Category category = CategoryFactory.Create().Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = category.Rename(newName: String.Empty);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NameException>();
	}

	[Test]
	public async Task Rename_WhenArchived_ShouldThrowArchivingException()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Archive();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = category.Rename(newName: "Продукты");
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task Archive_WithActiveCategory_ShouldSetIsArchivedTrue()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Archive();

		await Assert.That(value: category.IsArchived).IsTrue();
	}

	[Test]
	public async Task Archive_WhenAlreadyArchived_ShouldThrowArchivingException()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Archive();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = category.Archive();
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task Unarchive_WhenArchived_ShouldSetIsArchivedFalse()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Archive();
		category.Unarchive();

		await Assert.That(value: category.IsArchived).IsFalse();
	}

	[Test]
	public async Task Unarchive_WhenNotArchived_ShouldThrowUnarchivingException()
	{
		Category category = CategoryFactory.Create().Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = category.Unarchive();
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<UnarchivingException>();
	}
}