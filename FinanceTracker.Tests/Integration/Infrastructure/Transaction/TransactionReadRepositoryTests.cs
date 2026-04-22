using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Domains.Transaction.Events;
using FinanceTracker.Infrastructure.Database.Repositories;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transaction;

public sealed class TransactionReadRepositoryTests : DatabaseFixture
{
	private TransactionReadRepository _readRepository = null!;
	private TransactionWriteRepository _writeRepository = null!;
	private AccountWriteRepository _accountWriteRepository = null!;
	private CategoryRepository _categoryRepository = null!;

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		_readRepository = new TransactionReadRepository(context: Context);
		_writeRepository = new TransactionWriteRepository(context: Context);
		_accountWriteRepository = new AccountWriteRepository(context: Context);
		_categoryRepository = new CategoryRepository(context: Context);
	}
	
    private async Task<(Guid accountId, Guid categoryId)> CreateAccountAndCategoryAsync()
    {
        string currencyCode = await CreateCurrencyAsync();
        string accountType = await CreateAccountTypeAsync();
        Guid userId = await CreateUserAsync(currencyCode: currencyCode);

        AccountCreated accountEvent = new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: Guid.NewGuid(),
            UserId: userId,
            Name: "Карта Сбер",
            AccountType: accountType,
            Currency: currencyCode,
            Balance: 10000m,
            OccurredAt: DateTime.UtcNow
        );
        await _accountWriteRepository.CreateAsync(@event: accountEvent);

        Category category = Category.Create(
			userId: userId,
			name: "Еда",
			type: CategoryType.Expense,
			parentId: null
		);
		await _categoryRepository.CreateAsync(category: category);

        return (accountEvent.AccountId, category.Id);
    }
	
	private async Task<TransactionCreated> CreateTransactionAsync(
		Guid accountId,
		Guid categoryId,
		DirectionType direction = DirectionType.Debit,
		bool isExcluded = false,
		DateTime? occurredAt = null)
	{
		TransactionCreated @event = new TransactionCreated(
			Id: Guid.NewGuid(),
			TransactionId: Guid.NewGuid(),
			AccountId: accountId,
			UserId: Guid.NewGuid(),
			CategoryId: categoryId,
			Amount: 1000m,
			Direction: direction,
			ExchangeRate: 1m,
			Description: "Обед",
			OccurredAt: occurredAt ?? DateTime.UtcNow
		);

		await _writeRepository.CreateAsync(@event: @event);

		if (isExcluded)
			await _writeRepository.ExcludeAsync(@event: new TransactionExcluded(
				Id: Guid.NewGuid(),
				TransactionId: @event.TransactionId,
				OccurredAt: DateTime.UtcNow
			));

		return @event;
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
		Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(transactionId: Guid.NewGuid());

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_WithExistingTransaction_ShouldReturnCorrectTransaction()
	{
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		TransactionCreated @event = CreateTransactionCreatedEvent(accountId: accountId, categoryId: categoryId);
		await _writeRepository.CreateAsync(@event: @event);

		Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(transactionId: @event.TransactionId);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: @event.TransactionId);
		await Assert.That(value: result.AccountId).IsEqualTo(expected: accountId);
		await Assert.That(value: result.Amount).IsEqualTo(expected: 1000m);
		await Assert.That(value: result.Direction).IsEqualTo(expected: DirectionType.Debit);
		await Assert.That(value: result.Description).IsEqualTo(expected: "Обед");
		await Assert.That(value: result.IsExcluded).IsFalse();
	}
	
	[Test]
    public async Task GetAllAsync_WithNoTransactions_ShouldReturnEmptyList()
    {
        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(accountId: Guid.NewGuid());

        await Assert.That(value: result.Count).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnOnlyAccountTransactions()
    {
		(Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
		(Guid anotherAccountId, Guid anotherCategoryId) = await CreateAccountAndCategoryAsync();

		_ = await CreateTransactionAsync(accountId: accountId, categoryId: categoryId);
		_ = await CreateTransactionAsync(accountId: anotherAccountId, categoryId: anotherCategoryId);

		IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(accountId: accountId);

		await Assert.That(value: result.Count).IsEqualTo(expected: 1);
		await Assert.That(value: result[0].AccountId).IsEqualTo(expected: accountId);
    }

    [Test]
    public async Task GetAllAsync_WithDirectionFilter_ShouldReturnOnlyMatchingTransactions()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        _ = await CreateTransactionAsync(accountId: accountId, categoryId: categoryId, direction: DirectionType.Debit);
        _ = await CreateTransactionAsync(accountId: accountId, categoryId: categoryId, direction: DirectionType.Credit);

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            accountId: accountId,
            direction: DirectionType.Debit
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].Direction).IsEqualTo(expected: DirectionType.Debit);
    }

    [Test]
    public async Task GetAllAsync_WithIsExcludedFilter_ShouldReturnOnlyMatchingTransactions()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        _ = await CreateTransactionAsync(accountId: accountId, categoryId: categoryId, isExcluded: false);
        _ = await CreateTransactionAsync(accountId: accountId, categoryId: categoryId, isExcluded: true);

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            accountId: accountId,
            isExcluded: false
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].IsExcluded).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithCategoryIdFilter_ShouldReturnOnlyMatchingTransactions()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();

		Category category = Category.Create(
			userId: Guid.NewGuid(),
			name: "Транспорт",
			type: CategoryType.Expense,
			parentId: null
		);
		await _categoryRepository.CreateAsync(category: category);
		
        await Context.SaveChangesAsync();

        _ = await CreateTransactionAsync(accountId: accountId, categoryId: categoryId);
        _ = await CreateTransactionAsync(accountId: accountId, categoryId: category.Id);

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            accountId: accountId,
            categoryId: categoryId
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].CategoryId).IsEqualTo(expected: categoryId);
    }

    [Test]
    public async Task GetAllAsync_WithDateRangeFilter_ShouldReturnOnlyMatchingTransactions()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();

		_ = await CreateTransactionAsync(
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(-10)
        );
		_ = await CreateTransactionAsync(
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(-3)
        );
		_ = await CreateTransactionAsync(
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow
        );

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            accountId: accountId,
            dateFrom: DateTime.UtcNow.AddDays(value: -5),
            dateTo: DateTime.UtcNow.AddDays(value: 1)
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnTransactionsOrderedByDateDescending()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();

		_ = await CreateTransactionAsync(
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(-2)
        );
		_ = await CreateTransactionAsync(
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow
        );
		_ = await CreateTransactionAsync(
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(-1)
        );

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(accountId: accountId);

        await Assert.That(value: result[0].OccurredAt).IsGreaterThan(minimum: result[1].OccurredAt);
        await Assert.That(value: result[1].OccurredAt).IsGreaterThan(minimum: result[2].OccurredAt);
    }
}