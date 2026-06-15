using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context.Category;
using FinanceTracker.Infrastructure.Database.Context.Transaction;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Operation;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Transaction;

public sealed class TransactionWriteRepositoryTests : DatabaseFixture
{
	private TransactionWriteRepository _writeRepository = null!;
	private AccountWriteRepository _accountWriteRepository = null!;
	private CurrencyBuilder _currencyBuilder = null!;
	private UserBuilder _userBuilder = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_writeRepository = new TransactionWriteRepository(context: Context, operationRepository: new OperationWriteRepository(context: Context));
		_accountWriteRepository = new AccountWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		_currencyBuilder = new CurrencyBuilder(context: Context);
		_userBuilder = new UserBuilder(context: Context);
	}

	private async Task<(Guid accountId, Guid categoryId)> CreateAccountAndCategoryAsync()
	{
		Core.ValueObjects.Currency currency = await _currencyBuilder.CreateAsync();
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currency);

		Guid accountId = Guid.CreateVersion7();
		await _accountWriteRepository.CreateAsync(@event: new AccountCreated(
			Id: Guid.CreateVersion7(),
			AccountId: accountId,
			UserId: userId,
			Name: Name.Create(value: "Новый счёт").Value,
			Type: AccountType.Checking,
			Currency: currency,
			Balance: 10000m,
			Version: 1,
			OccurredAt: DateTimeOffset.UtcNow
		));

		Guid categoryId = Guid.CreateVersion7();
		await Context.Categories.AddAsync(entity: new CategoryEntity()
		{
			Id = categoryId,
			UserId = userId,
			ParentId = null,
			Name = Name.Create(value: "Еда").Value,
			Type = CategoryType.Expense,
			IsArchived = false,
			RowVersion = 0,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();

		return (accountId, categoryId);
	}

	private Core.Domains.Transaction.Transaction BuildTransaction(Guid accountId, Guid categoryId, string description = "тест")
	{
		return Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(),
			accountId: accountId,
			userId: Guid.CreateVersion7(),
			categoryId: categoryId,
			amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			isExcluded: false,
			description: description,
			isRatePending: false,
			rowVersion: 0,
			occurredAt: DateTimeOffset.UtcNow
		);
	}

	[Test]
	public async Task CreateAsync_ShouldCreateTransaction()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction transaction = BuildTransaction(accountId: accountId, categoryId: categoryId);

		await _writeRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		bool exists = await Context.Transactions.AnyAsync(predicate: t => t.Id == transaction.Id);
		await Assert.That(value: exists).IsTrue();
	}

	[Test]
	public async Task CreateAsync_ShouldSetCorrectValues()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
			id: Guid.CreateVersion7(),
			accountId: accountId,
			userId: Guid.CreateVersion7(),
			categoryId: categoryId,
			amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
			direction: DirectionType.Debit,
			exchangeRate: 1m,
			isExcluded: false,
			description: "тест",
			isRatePending: false,
			rowVersion: 0,
			occurredAt: DateTimeOffset.UtcNow
		);

		await _writeRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		TransactionEntity entity = await Context.Transactions.FirstAsync(predicate: t => t.Id == transaction.Id);

		await Assert.That(value: entity.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: entity.Direction).IsEqualTo(expected: DirectionType.Debit);
		await Assert.That(value: entity.Description).IsEqualTo(expected: "тест");
		await Assert.That(value: entity.IsExcluded).IsFalse();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task ChangeCategoryAsync_ShouldUpdateCategoryId()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction transaction = BuildTransaction(accountId: accountId, categoryId: categoryId);

		await _writeRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		Guid newCategoryId = Guid.CreateVersion7();
		await Context.Categories.AddAsync(entity: new CategoryEntity()
		{
			Id = newCategoryId,
			UserId = Guid.CreateVersion7(),
			ParentId = null,
			Name = Name.Create(value: "Развлечения").Value,
			Type = CategoryType.Expense,
			IsArchived = false,
			RowVersion = 0,
			CreatedAt = DateTimeOffset.UtcNow
		});
		await Context.SaveChangesAsync();

		await _writeRepository.ChangeCategoryAsync(
			transactionId: transaction.Id,
			userId: transaction.UserId,
			categoryId: newCategoryId,
			expectedVersion: 0
		);

		TransactionEntity entity = await Context.Transactions.AsNoTracking().FirstAsync(predicate: t => t.Id == transaction.Id);

		await Assert.That(value: entity.CategoryId).IsEqualTo(expected: newCategoryId);
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task ChangeDescriptionAsync_ShouldUpdateDescription()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction transaction = BuildTransaction(accountId: accountId, categoryId: categoryId, description: "старое");

		await _writeRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		await _writeRepository.ChangeDescriptionAsync(
			transactionId: transaction.Id,
			description: "новое",
			expectedVersion: 0
		);

		TransactionEntity entity = await Context.Transactions.AsNoTracking().FirstAsync(predicate: t => t.Id == transaction.Id);

		await Assert.That(value: entity.Description).IsEqualTo(expected: "новое");
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task ExcludeAsync_ShouldSetIsExcludedTrue()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction transaction = BuildTransaction(accountId: accountId, categoryId: categoryId);

		await _writeRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		await _writeRepository.ExcludeAsync(transactionId: transaction.Id, userId: transaction.UserId, expectedVersion: 0);

		TransactionEntity entity = await Context.Transactions.AsNoTracking().FirstAsync(predicate: t => t.Id == transaction.Id);

		await Assert.That(value: entity.IsExcluded).IsTrue();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task IncludeAsync_ShouldSetIsExcludedFalse()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction transaction = BuildTransaction(accountId: accountId, categoryId: categoryId);

		await _writeRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		await _writeRepository.ExcludeAsync(transactionId: transaction.Id, userId: transaction.UserId, expectedVersion: 0);
		await _writeRepository.IncludeAsync(transactionId: transaction.Id, userId: transaction.UserId, expectedVersion: 1);

		TransactionEntity entity = await Context.Transactions.AsNoTracking().FirstAsync(predicate: t => t.Id == transaction.Id);

		await Assert.That(value: entity.IsExcluded).IsFalse();
		await Assert.That(value: entity.RowVersion).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task ExcludeAsync_WhenVersionConflict_ShouldThrowConcurrencyConflictException()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		Core.Domains.Transaction.Transaction transaction = BuildTransaction(accountId: accountId, categoryId: categoryId);

		await _writeRepository.CreateAsync(transaction: transaction);
		await Context.SaveChangesAsync();

		await _writeRepository.ExcludeAsync(transactionId: transaction.Id, userId: transaction.UserId, expectedVersion: 0);

		await Assert.ThrowsAsync<ConcurrencyConflictException>(action: async () =>
			await _writeRepository.ExcludeAsync(transactionId: transaction.Id, userId: transaction.UserId, expectedVersion: 0)
		);
	}
}