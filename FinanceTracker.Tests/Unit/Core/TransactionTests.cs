using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Domains.Transactions.Events;
using FinanceTracker.Core.Exceptions;

namespace FinanceTracker.Tests.Unit.Core;

public sealed class TransactionTests 
{
	private static Transaction CreateTransaction(
		decimal amount = 1000m,
		DirectionType direction = DirectionType.Debit,
		decimal exchangeRate = 1m,
		string? description = null)
	{
		return Transaction.Create(
			accountId: Guid.NewGuid(),
			userId: Guid.NewGuid(),
			categoryId: Guid.NewGuid(),
			amount: amount,
			direction: direction,
			exchangeRate: exchangeRate,
			description: description,
			occurredAt: DateTime.UtcNow
		);
	}
	
	[Test]
	public async Task Create_WithValidData_ShouldSetCorrectState()
	{
		Guid accountId = Guid.NewGuid();
		Guid userId = Guid.NewGuid();
		Guid categoryId = Guid.NewGuid();
		DateTime occurredAt = DateTime.UtcNow;

		Transaction transaction = Transaction.Create(
			accountId: accountId,
			userId: userId,
			categoryId: categoryId,
			amount: 1000m,
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			description: "Обед",
			occurredAt: occurredAt
		);

		await Assert.That(value: transaction.Id).IsNotDefault();
		await Assert.That(value: transaction.AccountId).IsEqualTo(expected: accountId);
		await Assert.That(value: transaction.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: transaction.CategoryId).IsEqualTo(expected: categoryId);
		await Assert.That(value: transaction.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: transaction.Direction).IsEqualTo(expected: DirectionType.Debit);
		await Assert.That(value: transaction.ExchangeRate).IsEqualTo(expected: 1m);
		await Assert.That(value: transaction.Description).IsEqualTo(expected: "Обед");
		await Assert.That(value: transaction.IsExcluded).IsFalse();
		await Assert.That(value: transaction.OccurredAt).IsEqualTo(expected: occurredAt);
	}
	
	[Test]
    public async Task Create_WithZeroAmount_ShouldThrowInvalidAmountException()
        => await Assert.That(func: () => CreateTransaction(amount: 0)).Throws<InvalidAmountException>();

    [Test]
    public async Task Create_WithNegativeAmount_ShouldThrowInvalidAmountException()
        => await Assert.That(func: () => CreateTransaction(amount: -100m)).Throws<InvalidAmountException>();

    [Test]
    public async Task Create_WithZeroExchangeRate_ShouldThrowInvalidExchangeRateException()
        => await Assert.That(func: () => CreateTransaction(exchangeRate: 0)).Throws<InvalidExchangeRateException>();

    [Test]
    public async Task Create_WithNegativeExchangeRate_ShouldThrowInvalidExchangeRateException()
        => await Assert.That(func: () => CreateTransaction(exchangeRate: -1m)).Throws<InvalidExchangeRateException>();

    [Test]
    public async Task Create_ShouldRaiseTransactionCreatedEvent()
    {
        Transaction transaction = CreateTransaction();

        await Assert.That(value: transaction.Events.Count).IsEqualTo(expected: 1);
        await Assert.That(value: transaction.Events[0]).IsTypeOf<TransactionCreated>();
    }

    [Test]
    public async Task ChangeCategory_WithDifferentCategoryId_ShouldChangeCategoryId()
    {
        Transaction transaction = CreateTransaction();
        Guid newCategoryId = Guid.NewGuid();

        transaction.ChangeCategory(categoryId: newCategoryId);
        await Assert.That(value: transaction.CategoryId).IsEqualTo(expected: newCategoryId);
    }

    [Test]
    public async Task ChangeCategory_WithSameCategoryId_ShouldNotRaiseEvent()
    {
        Transaction transaction = CreateTransaction();
        transaction.ClearEvents();

        transaction.ChangeCategory(categoryId: transaction.CategoryId);

        await Assert.That(value: transaction.Events.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task ChangeDescription_WithDifferentDescription_ShouldChangeDescription()
    {
        Transaction transaction = CreateTransaction(description: "Обед");
        transaction.ClearEvents();

        transaction.ChangeDescription(description: "Ужин");

        await Assert.That(value: transaction.Description).IsEqualTo(expected: "Ужин");
        await Assert.That(value: transaction.Events.Count).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task ChangeDescription_WithSameDescription_ShouldNotRaiseEvent()
    {
        Transaction transaction = CreateTransaction(description: "Обед");
        transaction.ClearEvents();

        transaction.ChangeDescription(description: "Обед");

        await Assert.That(value: transaction.Events.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task Exclude_WhenIncluded_ShouldSetIsExcludedTrue()
    {
        Transaction transaction = CreateTransaction();

        transaction.Exclude();

        await Assert.That(value: transaction.IsExcluded).IsTrue();
    }

    [Test]
    public async Task Exclude_WhenAlreadyExcluded_ShouldThrowExcludingException()
    {
        Transaction transaction = CreateTransaction();
        transaction.Exclude();

        await Assert.That(action: transaction.Exclude).Throws<ExcludingException>();
    }

    [Test]
    public async Task Include_WhenExcluded_ShouldSetIsExcludedFalse()
    {
        Transaction transaction = CreateTransaction();
        transaction.Exclude();

        transaction.Include();

        await Assert.That(value: transaction.IsExcluded).IsFalse();
    }

    [Test]
    public async Task Include_WhenAlreadyIncluded_ShouldThrowIncludingException()
    {
        Transaction transaction = CreateTransaction();

        await Assert.That(action: transaction.Include).Throws<IncludingException>();
    }
}