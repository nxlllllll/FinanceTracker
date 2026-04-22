using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Account.Events;
using FinanceTracker.Core.Domains.Category;
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
        string currencyCode = await CreateCurrencyAsync();
        string accountType = await CreateAccountTypeAsync();
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

    [Test]
    public async Task CreateAsync_ShouldCreateTransaction()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            transactionId: transactionId,
            accountId: accountId,
            userId: Guid.NewGuid(),
            categoryId: categoryId,
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: "Обед",
            occurredAt: DateTime.UtcNow
        );

        bool exists = await Context.Transactions.AnyAsync(predicate: t => t.Id == transactionId);
        await Assert.That(value: exists).IsTrue();
    }

    [Test]
    public async Task CreateAsync_ShouldSetCorrectValues()
    {
        (Guid accountId, Guid categoryId) = await CreateAccountAndCategoryAsync();
        Guid transactionId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            transactionId: transactionId,
            accountId: accountId,
            userId: userId,
            categoryId: categoryId,
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: "Обед",
            occurredAt: DateTime.UtcNow
        );

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
        Guid transactionId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            transactionId: transactionId,
            accountId: accountId,
            userId: Guid.NewGuid(),
            categoryId: categoryId,
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: null,
            occurredAt: DateTime.UtcNow
        );

        Guid newCategoryId = Guid.NewGuid();
        await Context.Categories.AddAsync(entity: new CategoryEntity()
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
        Guid transactionId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            transactionId: transactionId,
            accountId: accountId,
            userId: Guid.NewGuid(),
            categoryId: categoryId,
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: "Обед",
            occurredAt: DateTime.UtcNow
        );

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
        Guid transactionId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            transactionId: transactionId,
            accountId: accountId,
            userId: Guid.NewGuid(),
            categoryId: categoryId,
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: null,
            occurredAt: DateTime.UtcNow
        );

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
        Guid transactionId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            transactionId: transactionId,
            accountId: accountId,
            userId: Guid.NewGuid(),
            categoryId: categoryId,
            amount: 1000m,
            direction: DirectionType.Debit,
            exchangeRate: 1m,
            description: null,
            occurredAt: DateTime.UtcNow
        );

        await _writeRepository.ExcludeAsync(transactionId: transactionId);
        await _writeRepository.IncludeAsync(transactionId: transactionId);

        bool isExcluded = await Context.Transactions
            .Where(predicate: t => t.Id == transactionId)
            .Select(selector: t => t.IsExcluded)
            .FirstAsync();

        await Assert.That(value: isExcluded).IsFalse();
    }
}