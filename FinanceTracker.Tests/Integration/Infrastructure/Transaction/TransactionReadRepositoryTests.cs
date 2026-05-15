using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transaction;

public sealed class TransactionReadRepositoryTests : DatabaseFixture
{
    private TransactionReadRepository _readRepository = null!;
    private TransactionWriteRepository _writeRepository = null!;
    private AccountWriteRepository _accountWriteRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;
    private AccountTypeBuilder _accountTypeBuilder = null!;
    private UserBuilder _userBuilder = null!;
    private AccountBuilder _accountBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;
    private TransactionBuilder _transactionBuilder = null!;
    private IUnitOfWork _unitOfWork = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _readRepository = new TransactionReadRepository(context: Context);
        _writeRepository = new TransactionWriteRepository(context: Context);
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            onError: Arg.Any<Func<Exception, Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
        _accountWriteRepository = new AccountWriteRepository(
            context: Context,
            dateProvider: FakeDateProvider.Default,
            unitOfWork: _unitOfWork,
            logger: Substitute.For<ILogger<AccountWriteRepository>>()
        );
        _currencyBuilder = new CurrencyBuilder(context: Context);
        _accountTypeBuilder = new AccountTypeBuilder(context: Context);
        _userBuilder = new UserBuilder(context: Context);
        _accountBuilder = new AccountBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
        _transactionBuilder = new TransactionBuilder(context: Context);
    }

    private async Task<(Guid accountId, Guid categoryId, Guid userId)> CreateAccountAndCategoryAsync()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        Guid accountId = Guid.CreateVersion7();
        await _accountWriteRepository.CreateAsync(@event: new AccountCreated(
            Id: Guid.CreateVersion7(),
            AccountId: accountId,
            UserId: userId,
            Name: Name.Create(value: "Карта Сбер").Value,
            Type: accountType,
            Currency: Core.ValueObjects.Currency.Create(value: currencyCode).Value,
            Balance: 10000m,
            OccurredAt: DateTime.UtcNow
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
            id: Guid.CreateVersion7(),
            accountId: accountId,
            userId: userId,
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
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
        Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(transactionId: Guid.CreateVersion7(), userId: Guid.CreateVersion7());

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

        Core.Domains.Transaction.Transaction? result = await _readRepository.GetByIdAsync(transactionId: transactionId, userId: userId);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Id).IsEqualTo(expected: transactionId);
        await Assert.That(value: result.AccountId).IsEqualTo(expected: accountId);
        await Assert.That(value: result.Amount.Amount).IsEqualTo(expected: 1000m);
        await Assert.That(value: result.Direction).IsEqualTo(expected: DirectionType.Debit);
        await Assert.That(value: result.IsExcluded).IsFalse();
    }

    [Test]
    public async Task ExistsAsync_WithNonExistentTransaction_ShouldReturnFalse()
    {
        bool result = await _readRepository.ExistsAsync(
            userId: Guid.CreateVersion7(),
            transactionId: Guid.CreateVersion7()
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
        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(accountId: Guid.CreateVersion7());

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
    
        [Test]
    public async Task GetAllAsync_WithoutCursor_ShouldReturnFirstPage()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        for (int i = 0; i < 5; i++)
            await _transactionBuilder.CreateAsync(
                userId: userId,
                accountId: accountId,
                categoryId: categoryId
            );

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            accountId: accountId,
            pageSize: 3
        );

        await Assert.That(value: result.Count).IsEqualTo(expected: 3);
    }

    [Test]
    public async Task GetAllAsync_WithCursor_ShouldReturnNextPage()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        DateTime baseTime = new DateTime(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

        for (int i = 0; i < 5; i++)
            await _transactionBuilder.CreateAsync(
                userId: userId,
                accountId: accountId,
                categoryId: categoryId,
                occurredAt: baseTime.AddHours(value: i)
            );

        IReadOnlyList<Core.Domains.Transaction.Transaction> firstPage = await _readRepository.GetAllAsync(
            accountId: accountId,
            pageSize: 3
        );

        Core.Domains.Transaction.Transaction lastItem = firstPage[^1];

        IReadOnlyList<Core.Domains.Transaction.Transaction> secondPage = await _readRepository.GetAllAsync(
            accountId: accountId,
            cursorOccurredAt: lastItem.OccurredAt,
            cursorId: lastItem.Id,
            pageSize: 3
        );

        await Assert.That(value: secondPage.Count).IsEqualTo(expected: 2);
        await Assert.That(value: secondPage.Any(t => firstPage.Any(f => f.Id == t.Id))).IsFalse();
    }

    [Test]
    public async Task GetAllAsync_WithCursor_ShouldNotReturnDuplicates()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        DateTime baseTime = new DateTime(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

        for (int i = 0; i < 6; i++)
            await _transactionBuilder.CreateAsync(
                userId: userId,
                accountId: accountId,
                categoryId: categoryId,
                occurredAt: baseTime.AddHours(value: i)
            );

        IReadOnlyList<Core.Domains.Transaction.Transaction> firstPage = await _readRepository.GetAllAsync(
            accountId: accountId,
            pageSize: 3
        );

        Core.Domains.Transaction.Transaction lastItem = firstPage[^1];

        IReadOnlyList<Core.Domains.Transaction.Transaction> secondPage = await _readRepository.GetAllAsync(
            accountId: accountId,
            cursorOccurredAt: lastItem.OccurredAt,
            cursorId: lastItem.Id,
            pageSize: 3
        );

        IEnumerable<Guid> allIds = firstPage.Select(t => t.Id).Concat(secondPage.Select(t => t.Id));
        await Assert.That(value: allIds.Distinct().Count()).IsEqualTo(expected: 6);
    }

    [Test]
    public async Task GetAllAsync_WhenNoMoreItems_ShouldReturnEmptyList()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        await _transactionBuilder.CreateAsync(
            userId: userId,
            accountId: accountId,
            categoryId: categoryId,
            occurredAt: new DateTime(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc)
        );

        IReadOnlyList<Core.Domains.Transaction.Transaction> firstPage = await _readRepository.GetAllAsync(
            accountId: accountId,
            pageSize: 3
        );

        Core.Domains.Transaction.Transaction lastItem = firstPage[^1];

        IReadOnlyList<Core.Domains.Transaction.Transaction> secondPage = await _readRepository.GetAllAsync(
            accountId: accountId,
            cursorOccurredAt: lastItem.OccurredAt,
            cursorId: lastItem.Id,
            pageSize: 3
        );

        await Assert.That(value: secondPage).IsEmpty();
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnItemsOrderedByOccurredAtDescending()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid accountId = await _accountBuilder.CreateAsync(userId: userId);
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        DateTime baseTime = new DateTime(year: 2025, month: 1, day: 1, hour: 0, minute: 0, second: 0, kind: DateTimeKind.Utc);

        for (int i = 0; i < 3; i++)
            await _transactionBuilder.CreateAsync(
                userId: userId,
                accountId: accountId,
                categoryId: categoryId,
                occurredAt: baseTime.AddHours(value: i)
            );

        IReadOnlyList<Core.Domains.Transaction.Transaction> result = await _readRepository.GetAllAsync(
            accountId: accountId,
            pageSize: 10
        );

        await Assert.That(value: result[0].OccurredAt).IsGreaterThan(minimum: result[1].OccurredAt);
        await Assert.That(value: result[1].OccurredAt).IsGreaterThan(minimum: result[2].OccurredAt);
    }
}