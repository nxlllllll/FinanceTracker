using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Domains.Transaction.Events;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.Repositories.Transaction;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Transaction;

public sealed class TransactionWriteRepositoryTests : DatabaseFixture
{
	private TransactionWriteRepository _writeRepository = null!;
    private AccountWriteRepository _accountWriteRepository = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
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

    private static TransactionCreated CreateTransactionCreatedEvent(
        Guid accountId,
        Guid categoryId,
        DirectionType direction = DirectionType.Debit)
    {
        return new TransactionCreated(
            Id: Guid.NewGuid(),
            TransactionId: Guid.NewGuid(),
            AccountId: accountId,
            UserId: Guid.NewGuid(),
            CategoryId: categoryId,
            Amount: 1000m,
            Direction: direction,
            ExchangeRate: 1m,
            Description: "Обед",
            OccurredAt: DateTime.UtcNow
        );
    }

    [Test]
    public async Task CreateAsync_ShouldCreateTransaction()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        TransactionCreated @event = CreateTransactionCreatedEvent(
            accountId: accountId,
            categoryId: categoryId
        );

        await _writeRepository.CreateAsync(@event: @event);

        bool exists = await Context.Transactions.AnyAsync(predicate: t => t.Id == @event.TransactionId);
        await Assert.That(value: exists).IsTrue();
    }

    [Test]
    public async Task ChangeCategoryAsync_ShouldUpdateCategoryId()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        TransactionCreated created = CreateTransactionCreatedEvent(
            accountId: accountId,
            categoryId: categoryId
        );
        await _writeRepository.CreateAsync(@event: created);

        Guid newCategoryId = Guid.NewGuid();
        await Context.Categories.AddAsync(new CategoryEntity()
        {
            Id = newCategoryId,
            UserId = Guid.NewGuid(),
            ParentId = null,
            Name = "Транспорт",
            Type = CategoryType.Expense,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        await _writeRepository.ChangeCategoryAsync(@event: new TransactionCategoryChanged(
            Id: Guid.NewGuid(),
            TransactionId: created.TransactionId,
            CategoryId: newCategoryId,
            OccurredAt: DateTime.UtcNow
        ));

        Guid? loadedCategoryId = await Context.Transactions
            .Where(predicate: t => t.Id == created.TransactionId)
            .Select(selector: t => t.CategoryId)
            .FirstOrDefaultAsync();

        await Assert.That(value: loadedCategoryId).IsEqualTo(expected: newCategoryId);
    }

    [Test]
    public async Task ChangeDescriptionAsync_ShouldUpdateDescription()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        TransactionCreated created = CreateTransactionCreatedEvent(accountId: accountId, categoryId: categoryId);
        await _writeRepository.CreateAsync(@event: created);

        await _writeRepository.ChangeDescriptionAsync(@event: new TransactionDescriptionChanged(
            Id: Guid.NewGuid(),
            TransactionId: created.TransactionId,
            Description: "Ужин",
            OccurredAt: DateTime.UtcNow
        ));

        string? description = await Context.Transactions
            .Where(predicate: t => t.Id == created.TransactionId)
            .Select(selector: t => t.Description)
            .FirstOrDefaultAsync();

        await Assert.That(value: description).IsEqualTo(expected: "Ужин");
    }

    [Test]
    public async Task ExcludeAsync_ShouldSetIsExcludedTrue()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        TransactionCreated created = CreateTransactionCreatedEvent(accountId: accountId, categoryId: categoryId);
        await _writeRepository.CreateAsync(@event: created);

        await _writeRepository.ExcludeAsync(@event: new TransactionExcluded(
            Id: Guid.NewGuid(),
            TransactionId: created.TransactionId,
            OccurredAt: DateTime.UtcNow
        ));

        bool isExcluded = await Context.Transactions
            .Where(predicate: t => t.Id == created.TransactionId)
            .Select(selector: t => t.IsExcluded)
            .FirstAsync();

        await Assert.That(value: isExcluded).IsTrue();
    }

    [Test]
    public async Task IncludeAsync_ShouldSetIsExcludedFalse()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        TransactionCreated created = CreateTransactionCreatedEvent(accountId: accountId, categoryId: categoryId);
        await _writeRepository.CreateAsync(@event: created);

        await _writeRepository.ExcludeAsync(@event: new TransactionExcluded(
            Id: Guid.NewGuid(),
            TransactionId: created.TransactionId,
            OccurredAt: DateTime.UtcNow
        ));

        await _writeRepository.IncludeAsync(@event: new TransactionIncluded(
            Id: Guid.NewGuid(),
            TransactionId: created.TransactionId,
            OccurredAt: DateTime.UtcNow
        ));

        bool isExcluded = await Context.Transactions
            .Where(predicate: t => t.Id == created.TransactionId)
            .Select(selector: t => t.IsExcluded)
            .FirstAsync();

        await Assert.That(value: isExcluded).IsFalse();
    }
}