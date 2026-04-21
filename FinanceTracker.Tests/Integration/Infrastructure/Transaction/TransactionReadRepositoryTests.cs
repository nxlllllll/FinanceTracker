using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Transactions;
using FinanceTracker.Core.Domains.Transactions.Events;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transaction;

public sealed class TransactionReadRepositoryTests : DatabaseFixture
{
	private TransactionReadRepository _readRepository = null!;
	private TransactionWriteRepository _writeRepository = null!;
	private AccountWriteRepository _accountWriteRepository = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new TransactionReadRepository(context: Context);
		_writeRepository = new TransactionWriteRepository(context: Context);
		_accountWriteRepository = new AccountWriteRepository(context: Context);
	}
	
	private async Task<(Guid accountId, Guid categoryId)> CreateAccountAndCategoryAsync()
	{
		string currencyCode = await CreateCurrencyAsync(code: "RUB");
		string accountType = await CreateAccountTypeAsync(type: "checking");
		Guid userId = await CreateUserAsync(currencyCode: currencyCode);

		Guid accountId = Guid.NewGuid();
		await _accountWriteRepository.CreateAsync(@event: new AccountCreated(
			Id: Guid.NewGuid(),
			AccountId: accountId,
			UserId: userId,
			Name: "Карта Сбер",
			AccountType: accountType,
			Currency: currencyCode,
			Balance: 10000m,
			OccurredAt: DateTime.UtcNow
		));

		Guid categoryId = Guid.NewGuid();
		await Context.Categories.AddAsync(entity: new CategoryEntity()
		{
			Id = categoryId,
			UserId = userId,
			ParentId = null,
			Name = "Еда",
			Type = CategoryType.Expense,
			IsArchived = false,
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();

		return (accountId, categoryId);
	}

	private static TransactionCreated CreateTransactionCreatedEvent(Guid accountId, Guid categoryId)
	{
		return new TransactionCreated(
			Id: Guid.NewGuid(),
			TransactionId: Guid.NewGuid(),
			AccountId: accountId,
			UserId: Guid.NewGuid(),
			CategoryId: categoryId,
			Amount: 1000m,
			Direction: DirectionType.Debit,
			ExchangeRate: 1m,
			Description: "Обед",
			OccurredAt: DateTime.UtcNow
		);
	}

	[Test]
	public async Task GetByIdAsync_WithNonExistentTransaction_ShouldReturnNull()
	{
		FinanceTracker.Core.Domains.Transactions.Transaction? result = await _readRepository.GetByIdAsync(transactionId: Guid.NewGuid());

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_WithExistingTransaction_ShouldReturnCorrectTransaction()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		TransactionCreated @event = CreateTransactionCreatedEvent(accountId: accountId, categoryId: categoryId);
		await _writeRepository.CreateAsync(@event: @event);

		FinanceTracker.Core.Domains.Transactions.Transaction? result = await _readRepository.GetByIdAsync(transactionId: @event.TransactionId);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: @event.TransactionId);
		await Assert.That(value: result.AccountId).IsEqualTo(expected: accountId);
		await Assert.That(value: result.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: result.Direction).IsEqualTo(expected: DirectionType.Debit);
		await Assert.That(value: result.Description).IsEqualTo(expected: "Обед");
		await Assert.That(value: result.IsExcluded).IsFalse();
	}
}