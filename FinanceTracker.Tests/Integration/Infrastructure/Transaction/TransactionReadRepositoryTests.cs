using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transaction;

public sealed class TransactionReadRepositoryTests : DatabaseFixture
{
    private TransactionReadRepository _readRepository = null!;
    private TransactionWriteRepository _writeRepository = null!;
    private AccountWriteRepository _accountWriteRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;
    private AccountTypeBuilder _accountTypeBuilder = null!;
    private UserBuilder _userBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _readRepository = new TransactionReadRepository(context: Context);
        _writeRepository = new TransactionWriteRepository(context: Context);
        _accountWriteRepository = new AccountWriteRepository(context: Context);
        _currencyBuilder = new CurrencyBuilder(context: Context);
        _accountTypeBuilder = new AccountTypeBuilder(context: Context);
        _userBuilder = new UserBuilder(context: Context);
    }

    private async Task<(Guid accountId, Guid categoryId, Guid userId)> CreateAccountAndCategoryAsync()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        Guid accountId = Guid.NewGuid();
        await _accountWriteRepository.CreateAsync(@event: new AccountCreated(
            Id: Guid.NewGuid(),
            AccountId: accountId,
            UserId: userId,
            Name: "Карта Сбер",
            Type: accountType,
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

        return (accountId, categoryId, userId);
    }

    private async Task<Guid> CreateTransactionAsync(
        Guid accountId,
        Guid categoryId,
        Guid userId,
        DirectionType direction = DirectionType.Debit,
        bool isExcluded = false,
        DateTime? occurredAt = null)
    {
        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
            id: Guid.NewGuid(),
            accountId: accountId,
            userId: userId,
            categoryId: categoryId,
            amount: 1000m,
            currency: "RUB",
            direction: direction,
            exchangeRate: 1m,
            isExcluded: false,
            isRatePending: false,
            description: "Обед",
            occurredAt: occurredAt ?? DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(transaction: transaction);

        if (isExcluded)
            await _writeRepository.ExcludeAsync(transactionId: transaction.Id);

        return transaction.Id;
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentTransaction_ShouldReturnNull()
    {
        Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(transactionId: Guid.NewGuid());

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_WithExistingTransaction_ShouldReturnCorrectDto()
    {
        (Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId
        );

        Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(transactionId: transactionId);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Id).IsEqualTo(expected: transactionId);
        await Assert.That(value: result.AccountId).IsEqualTo(expected: accountId);
        await Assert.That(value: result.Amount).IsEqualTo(expected: 1000m);
        await Assert.That(value: result.Direction).IsEqualTo(expected: DirectionType.Debit);
        await Assert.That(value: result.IsExcluded).IsFalse();
    }

    [Test]
    public async Task ExistsAsync_WithNonExistentTransaction_ShouldReturnFalse()
    {
        bool result = await _readRepository.ExistsAsync(
            userId: Guid.NewGuid(),
            transactionId: Guid.NewGuid()
        );

        await Assert.That(value: result).IsFalse();
    }

    [Test]
    public async Task ExistsAsync_WithExistingTransaction_ShouldReturnTrue()
    {
        (Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId
        );

        bool result = await _readRepository.ExistsAsync(
            userId: userId,
            transactionId: transactionId
        );

        await Assert.That(value: result).IsTrue();
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
        (Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();
        (Guid anotherAccountId, Guid anotherCategoryId, Guid anotherUserId) = await CreateAccountAndCategoryAsync();

        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId
        );
        await CreateTransactionAsync(
            userId: anotherUserId,
            accountId: anotherAccountId,
            categoryId: anotherCategoryId
        );

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(accountId: accountId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].AccountId).IsEqualTo(expected: accountId);
    }

    [Test]
    public async Task GetAllAsync_WithDirectionFilter_ShouldReturnOnlyMatchingTransactions()
    {
        (Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            direction: DirectionType.Debit
        );
        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            direction: DirectionType.Credit
        );

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
        (Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            isExcluded: false
        );
        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            isExcluded: true
        );

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            accountId: accountId,
            isExcluded: false
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 1);
        await Assert.That(value: result[0].IsExcluded).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithDateRangeFilter_ShouldReturnOnlyMatchingTransactions()
    {
        (Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(value: -10)
        );
        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(value: -3)
        );
        await CreateTransactionAsync(
            userId: userId,
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
        (Guid accountId, Guid categoryId, Guid userId) = await CreateAccountAndCategoryAsync();

        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(value: -2)
        );
        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow
        );
        await CreateTransactionAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: DateTime.UtcNow.AddDays(value: -1)
        );

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(accountId: accountId);

        await Assert.That(value: result[0].OccurredAt).IsGreaterThan(minimum: result[1].OccurredAt);
        await Assert.That(value: result[1].OccurredAt).IsGreaterThan(minimum: result[2].OccurredAt);
    }
}