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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transaction;

public sealed class TransactionWriteRepositoryTests : DatabaseFixture
{
    private TransactionWriteRepository _writeRepository = null!;
    private AccountWriteRepository _accountWriteRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;
    private UserBuilder _userBuilder = null!;
    private IUnitOfWork _unitOfWork = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
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
        _userBuilder = new UserBuilder(context: Context);
    }

    private async Task<(Guid accountId, Guid categoryId)> CreateAccountAndCategoryAsync()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        Guid accountId = Guid.CreateVersion7();
        await _accountWriteRepository.CreateAsync(@event: new AccountCreated(
            Id: Guid.CreateVersion7(),
            AccountId: accountId,
            UserId: userId,
            Name: Name.Create(value: "Карта Сбер").Value,
            Type: AccountType.Checking,
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

        return (accountId, categoryId);
    }

    [Test]
    public async Task CreateAsync_ShouldCreateTransaction()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.CreateVersion7();

        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
            id: transactionId,
            accountId: accountId,
            userId: Guid.CreateVersion7(),
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            isExcluded: false,
            description: "Обед",
            isRatePending: false,
            occurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(transaction: transaction);

        bool exists = await Context.Transactions.AnyAsync(predicate: t => t.Id == transactionId);
        await Assert.That(value: exists).IsTrue();
    }

    [Test]
    public async Task CreateAsync_ShouldSetCorrectValues()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.CreateVersion7();
        Guid userId = Guid.CreateVersion7();

        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
            id: transactionId,
            accountId: accountId,
            userId: userId,
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            isExcluded: false,
            description: "Обед",
            isRatePending: false,
            occurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(transaction: transaction);
        
        TransactionEntity entity = await Context.Transactions.FirstAsync(
            predicate: t => t.Id == transactionId
        );

        await Assert.That(value: entity.Amount).IsEqualTo(expected: 1000m);
        await Assert.That(value: entity.Direction).IsEqualTo(expected: DirectionType.Debit);
        await Assert.That(value: entity.Description).IsEqualTo(expected: "Обед");
        await Assert.That(value: entity.IsExcluded).IsFalse();
        await Assert.That(value: entity.UserId).IsEqualTo(expected: userId);
    }

    [Test]
    public async Task ChangeCategoryAsync_ShouldUpdateCategoryId()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.CreateVersion7();

        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
            id: transactionId,
            accountId: accountId,
            userId: Guid.CreateVersion7(),
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            isExcluded: false,
            description: null,
            isRatePending: false,
            occurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(transaction: transaction);

        Guid newCategoryId = Guid.CreateVersion7();
        await Context.Categories.AddAsync(entity: new CategoryEntity()
        {
            Id = newCategoryId,
            UserId = Guid.CreateVersion7(),
            ParentId = null,
            Name = Name.Create(value: "Транспорт").Value,
            Type = CategoryType.Expense,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        await _writeRepository.ChangeCategoryAsync(
            transactionId: transactionId,
            categoryId: newCategoryId
        );

        Guid loadedCategoryId = await Context.Transactions
            .Where(predicate: t => t.Id == transactionId)
            .Select(selector: t => t.CategoryId)
            .FirstAsync();

        await Assert.That(value: loadedCategoryId).IsEqualTo(expected: newCategoryId);
    }

    [Test]
    public async Task ChangeDescriptionAsync_ShouldUpdateDescription()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.CreateVersion7();

        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
            id: transactionId,
            accountId: accountId,
            userId: Guid.CreateVersion7(),
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            isExcluded: false,
            description: "Обед",
            isRatePending: false,
            occurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(transaction: transaction);

        await _writeRepository.ChangeDescriptionAsync(transactionId: transactionId, description: "Ужин");

        string? description = await Context.Transactions
            .Where(predicate: t => t.Id == transactionId)
            .Select(selector: t => t.Description)
            .FirstAsync();

        await Assert.That(value: description).IsEqualTo(expected: "Ужин");
    }

    [Test]
    public async Task ExcludeAsync_ShouldSetIsExcludedTrue()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.CreateVersion7();

        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
            id: transactionId,
            accountId: accountId,
            userId: Guid.CreateVersion7(),
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            isExcluded: false,
            description: null,
            isRatePending: false,
            occurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(transaction: transaction);

        await _writeRepository.ExcludeAsync(transactionId: transactionId);

        bool isExcluded = await Context.Transactions
            .Where(predicate: t => t.Id == transactionId)
            .Select(selector: t => t.IsExcluded)
            .FirstAsync();

        await Assert.That(value: isExcluded).IsTrue();
    }

    [Test]
    public async Task IncludeAsync_ShouldSetIsExcludedFalse()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.CreateVersion7();

        Core.Domains.Transaction.Transaction transaction = Core.Domains.Transaction.Transaction.Reconstitute(
            id: transactionId,
            accountId: accountId,
            userId: Guid.CreateVersion7(),
            categoryId: categoryId,
            amount: Money.Create(amount: 1000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            isExcluded: false,
            description: null,
            isRatePending: false,
            occurredAt: DateTime.UtcNow
        );

        await _writeRepository.CreateAsync(transaction: transaction);

        await _writeRepository.ExcludeAsync(transactionId: transactionId);
        await _writeRepository.IncludeAsync(transactionId: transactionId);

        bool isExcluded = await Context.Transactions
            .Where(predicate: t => t.Id == transactionId)
            .Select(selector: t => t.IsExcluded)
            .FirstAsync();

        await Assert.That(value: isExcluded).IsFalse();
    }
}