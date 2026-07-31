using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Domains;

public sealed class TransactionTests
{
	[Test]
	public async Task Create_ShouldSetIsExcludedToFalse()
	{
		Transaction transaction = TransactionFactory.Create();

		await Assert.That(value: transaction.IsExcluded).IsFalse();
	}

	[Test]
	public async Task Create_ShouldGenerateUniqueId()
	{
		Transaction first = TransactionFactory.Create();
		Transaction second = TransactionFactory.Create();

		await Assert.That(value: first.Id).IsNotEqualTo(notExpected: second.Id);
	}

	[Test]
	public async Task Create_ShouldSetDirectionCorrectly()
	{
		Transaction transaction = TransactionFactory.Create(direction: DirectionType.Credit);

		await Assert.That(value: transaction.Direction).IsEqualTo(expected: DirectionType.Credit);
	}

	[Test]
	public async Task Exclude_ActiveTransaction_ShouldSetIsExcluded()
	{
		Transaction transaction = TransactionFactory.Create();

		transaction.Exclude();

		await Assert.That(value: transaction.IsExcluded).IsTrue();
	}

	[Test]
	public async Task Exclude_AlreadyExcludedTransaction_ShouldReportNoChange()
	{
		Transaction transaction = TransactionFactory.Create(isExcluded: true);

		Result<bool, DomainException> result = transaction.Exclude();

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsFalse();
		await Assert.That(value: transaction.IsExcluded).IsTrue();
	}

	[Test]
	public async Task Include_ExcludedTransaction_ShouldClearIsExcludedAndReportChange()
	{
		Transaction transaction = TransactionFactory.Create(isExcluded: true);

		Result<bool, DomainException> result = transaction.Include();

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsTrue();
		await Assert.That(value: transaction.IsExcluded).IsFalse();
	}

	[Test]
	public async Task Include_ActiveTransaction_ShouldReportNoChange()
	{
		Transaction transaction = TransactionFactory.Create();

		Result<bool, DomainException> result = transaction.Include();

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsFalse();
		await Assert.That(value: transaction.IsExcluded).IsFalse();
	}

	[Test]
	public async Task ChangeCategory_ShouldUpdateCategoryId()
	{
		Transaction transaction = TransactionFactory.Create();
		Guid newCategoryId = Guid.CreateVersion7();

		transaction.ChangeCategory(categoryId: newCategoryId);

		await Assert.That(value: transaction.CategoryId).IsEqualTo(expected: newCategoryId);
	}

	[Test]
	public async Task ChangeCategory_WithTheSameCategory_ShouldReportNoChange()
	{
		Transaction transaction = TransactionFactory.Create();

		Result<bool, DomainException> result = transaction.ChangeCategory(categoryId: transaction.CategoryId);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsFalse();
	}

	[Test]
	public async Task ChangeCategory_WhenExcluded_ShouldReturnExcludedOperationException()
	{
		Transaction transaction = TransactionFactory.Create(isExcluded: true);

		Result<bool, DomainException> result = transaction.ChangeCategory(categoryId: Guid.CreateVersion7());

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ExcludedOperationException>();
	}

	[Test]
	public async Task ChangeDescription_ShouldUpdateDescription()
	{
		Transaction transaction = TransactionFactory.Create();

		transaction.ChangeDescription(description: "Тест");

		await Assert.That(value: transaction.Description).IsEqualTo(expected: "Тест");
	}

	[Test]
	public async Task ChangeDescription_WithTheSameDescription_ShouldReportNoChange()
	{
		Transaction transaction = TransactionFactory.Create();

		Result<bool, DomainException> result = transaction.ChangeDescription(description: transaction.Description);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsFalse();
	}

	[Test]
	public async Task ChangeDescription_WithNull_ShouldClearDescription()
	{
		Transaction transaction = TransactionFactory.Create();

		transaction.ChangeDescription(description: null);

		await Assert.That(value: transaction.Description).IsNull();
	}

	[Test]
	public async Task ChangeDescription_WhenExcluded_ShouldReturnExcludedOperationException()
	{
		Transaction transaction = TransactionFactory.Create(isExcluded: true);

		Result<bool, DomainException> result = transaction.ChangeDescription(description: "попытка изменить");

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ExcludedOperationException>();
	}
}
