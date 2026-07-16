using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class CategoryTests
{
	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid userId = Guid.CreateVersion7();
		Category category = CategoryFactory.Create(userId: userId).Value!;

		await Assert.That(value: category.Id).IsNotDefault();
		await Assert.That(value: category.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: category.Name.Value).IsEqualTo(expected: "Еда");
		await Assert.That(value: category.Type).IsEqualTo(expected: CategoryType.Expense);
		await Assert.That(value: category.ParentId).IsNull();
		await Assert.That(value: category.IsArchived).IsFalse();
		await Assert.That(value: category.CreatedAt).IsNotDefault();
	}

	[Test]
	public async Task Create_WithParentId_ShouldSetParentId()
	{
		Guid parentId = Guid.CreateVersion7();
		Category category = CategoryFactory.Create(parentId: parentId).Value!;

		await Assert.That(value: category.ParentId).IsEqualTo(expected: parentId);
	}

	[Test]
	public async Task Rename_WithValidName_ShouldChangeName()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Rename(newName: Name.Create(value: "Транспорт").Value);

		await Assert.That(value: category.Name.Value).IsEqualTo(expected: "Транспорт");
	}

	[Test]
	public async Task Rename_WithSameName_ShouldNotChangeName()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Rename(newName: Name.Create(value: "Еда").Value);

		await Assert.That(value: category.Name.Value).IsEqualTo(expected: "Еда");
	}

	[Test]
	public async Task Rename_WhenArchived_ShouldThrowArchivingException()
	{
		Category category = CategoryFactory.Create().Value!;
		category.Archive();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = category.Rename(
			newName: Name.Create(value: "Транспорт").Value
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task Archive_ActiveCategory_ShouldSetIsArchived()
	{
		Category category = CategoryFactory.Create().Value!;

		category.Archive();

		await Assert.That(value: category.IsArchived).IsTrue();
	}

	[Test]
	public async Task Archive_AlreadyArchivedCategory_ShouldThrowArchivingException()
	{
		Category category = CategoryFactory.Create().Value!;
		category.Archive();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = category.Archive();

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task Unarchive_ArchivedCategory_ShouldClearIsArchived()
	{
		Category category = CategoryFactory.Create().Value!;
		category.Archive();

		category.Unarchive();

		await Assert.That(value: category.IsArchived).IsFalse();
	}

	[Test]
	public async Task Unarchive_ActiveCategory_ShouldThrowUnarchivingException()
	{
		Category category = CategoryFactory.Create().Value!;

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = category.Unarchive();

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<UnarchivingException>();
	}
}
