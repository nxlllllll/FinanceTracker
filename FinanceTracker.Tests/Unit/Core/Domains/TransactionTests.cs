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
	public async Task Exclude_AlreadyExcludedTransaction_ShouldThrowExcludingException()
	{
		Transaction transaction = TransactionFactory.Create(isExcluded: true);

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transaction.Exclude();
        
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ExcludingException>();
	}

	[Test]
	public async Task Include_ExcludedTransaction_ShouldClearIsExcluded()
	{
		Transaction transaction = TransactionFactory.Create(isExcluded: true);

		transaction.Include();

		await Assert.That(value: transaction.IsExcluded).IsFalse();
	}

	[Test]
	public async Task Include_ActiveTransaction_ShouldThrowIncludingException()
	{
		Transaction transaction = TransactionFactory.Create();

		Result<FinanceTracker.Core.Results.Unit, DomainException> result = transaction.Include();
        
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<IncludingException>();
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
	public async Task ChangeDescription_ShouldUpdateDescription()
	{
		Transaction transaction = TransactionFactory.Create();

		transaction.ChangeDescription(description: "Ужин");

		await Assert.That(value: transaction.Description).IsEqualTo(expected: "Ужин");
	}

	[Test]
	public async Task ChangeDescription_WithNull_ShouldClearDescription()
	{
		Transaction transaction = TransactionFactory.Create();

		transaction.ChangeDescription(description: null);

		await Assert.That(value: transaction.Description).IsNull();
	}
}