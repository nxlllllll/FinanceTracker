using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Category;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class CategoryParentTests
{
	private static Category Expense(Guid? parentId = null) => CategoryFactory.Create(type: CategoryType.Expense, parentId: parentId).Value!;

	[Test]
	public async Task Create_UnderAParentOfTheOtherType_ShouldFail()
	{
		Result<Category, DomainException> result = CategoryFactory.Create(
			type: CategoryType.Income,
			parentId: Guid.CreateVersion7(),
			parentType: CategoryType.Expense
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<CategoryTypeMismatchException>().Because(message: """
			The income and expense summary groups by the type of each category, so a tree mixing both
			reports a total that belongs to neither. Rejecting the pair is what keeps a subtree summable.
		""");
	}

	[Test]
	public async Task ChangeParent_ToItself_ShouldFail()
	{
		Category category = Expense();

		Result<bool, DomainException> result = category.ChangeParent(newParentId: category.Id, newParentType: CategoryType.Expense);

		await Assert.That(value: result.Error).IsTypeOf<CategoryCycleException>();
	}

	[Test]
	public async Task ChangeParent_ToAParentOfTheOtherType_ShouldFail()
	{
		Category category = Expense();

		Result<bool, DomainException> result = category.ChangeParent(newParentId: Guid.CreateVersion7(), newParentType: CategoryType.Income);

		await Assert.That(value: result.Error).IsTypeOf<CategoryTypeMismatchException>();
	}

	[Test]
	public async Task ChangeParent_OfAnArchivedCategory_ShouldFail()
	{
		Category category = CategoryFactory.Create(type: CategoryType.Expense, archived: true).Value!;

		Result<bool, DomainException> result = category.ChangeParent(newParentId: Guid.CreateVersion7(), newParentType: CategoryType.Expense);

		await Assert.That(value: result.Error).IsTypeOf<ArchivedOperationException>();
	}

	[Test]
	public async Task ChangeParent_ToTheRoot_ShouldClearTheParent()
	{
		Category category = Expense(parentId: Guid.CreateVersion7());

		Result<bool, DomainException> result = category.ChangeParent(newParentId: null, newParentType: null);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsTrue();
		await Assert.That(value: category.ParentId).IsNull().Because(message: """
			Moving to the root is an ordinary move, not a special case: a category with no parent is
			exactly what a root is.
		""");
	}

	[Test]
	public async Task ChangeParent_ToTheSameParent_ShouldReportNoChange()
	{
		Guid parentId = Guid.CreateVersion7();
		Category category = Expense(parentId: parentId);

		Result<bool, DomainException> result = category.ChangeParent(newParentId: parentId, newParentType: CategoryType.Expense);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsFalse().Because(message: """
			A move that changes nothing must not spend a row version or stage a notification claiming
			the category moved.
		""");
	}
}
