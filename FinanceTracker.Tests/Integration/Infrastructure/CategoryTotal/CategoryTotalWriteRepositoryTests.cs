using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.CategoryTotal;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.CategoryTotal;

public sealed class CategoryTotalWriteRepositoryTests : DatabaseFixture
{
    private CategoryTotalWriteRepository _writeRepository = null!;
    private CurrencyBuilder _currencyBuilder = null!;
    private UserBuilder _userBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _writeRepository = new CategoryTotalWriteRepository(context: Context);
        _currencyBuilder = new CurrencyBuilder(context: Context);
        _userBuilder = new UserBuilder(context: Context);
    }

    private async Task<(Guid userId, Guid categoryId)> CreateUserAndCategoryAsync()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

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

        return (userId, categoryId);
    }

    [Test]
    public async Task AddAsync_WhenNoRecordExists_ShouldCreateNewRecord()
    {
        (Guid userId, Guid categoryId) = await CreateUserAndCategoryAsync();

        await _writeRepository.AddAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 1000m,
            occurredAt: new DateTime(
                year: 2025, 
                month: 1,
                day: 15,
                hour: 0,
                minute: 0,
                second: 0,
                kind: DateTimeKind.Utc
            )
        );

        CategoryTotalEntity? entity = await Context.CategoryTotals.FirstOrDefaultAsync(predicate: ct =>
            ct.UserId == userId &&
            ct.CategoryId == categoryId &&
            ct.Period == new DateOnly(year: 2025, month: 1, day: 1)
        );

        await Assert.That(value: entity).IsNotNull();
        await Assert.That(value: entity!.Total).IsEqualTo(expected: 1000m);
        await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 1);
    }

    [Test]
    public async Task AddAsync_WhenRecordExists_ShouldAccumulateTotal()
    {
        (Guid userId, Guid categoryId) = await CreateUserAndCategoryAsync();
        DateTime occurredAt = new DateTime(
            year: 2025,
            month: 1,
            day: 15,
            hour: 0,
            minute: 0,
            second: 0,
            kind: DateTimeKind.Utc
        );

        await _writeRepository.AddAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 1000m,
            occurredAt: occurredAt
        );
        
        await _writeRepository.AddAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 500m,
            occurredAt: occurredAt
        );

        CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
            predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
        );

        await Assert.That(value: entity.Total).IsEqualTo(expected: 1500m);
        await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task SubtractAsync_ShouldDecreaseTotal()
    {
        (Guid userId, Guid categoryId) = await CreateUserAndCategoryAsync();
        DateTime occurredAt = new DateTime(
            year: 2025,
            month: 1,
            day: 15,
            hour: 0,
            minute: 0,
            second: 0,
            kind: DateTimeKind.Utc
        );

        await _writeRepository.AddAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 1000m,
            occurredAt: occurredAt
        );
        await _writeRepository.SubtractAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 400m,
            occurredAt: occurredAt
        );

        CategoryTotalEntity entity = await Context.CategoryTotals.FirstAsync(
            predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
        );

        await Assert.That(value: entity.Total).IsEqualTo(expected: 600m);
        await Assert.That(value: entity.TransactionCount).IsEqualTo(expected: 0);
    }

    [Test]
    public async Task AddAsync_ShouldSeparatePeriodsByMonth()
    {
        (Guid userId, Guid categoryId) = await CreateUserAndCategoryAsync();

        await _writeRepository.AddAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 1000m,
            occurredAt: new DateTime(
                year: 2025,
                month: 1,
                day: 15,
                hour: 0,
                minute: 0,
                second: 0,
                kind: DateTimeKind.Utc)
        );
        await _writeRepository.AddAsync(
            userId: userId,
            categoryId: categoryId,
            amount: 2000m,
            occurredAt: new DateTime(
                 year: 2025,
                 month: 2,
                 day: 10,
                 hour: 0,
                 minute: 0,
                 second: 0,
                 kind: DateTimeKind.Utc)
        );

        int count = await Context.CategoryTotals.CountAsync(
            predicate: ct => ct.UserId == userId && ct.CategoryId == categoryId
        );

        await Assert.That(value: count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task MoveCategoryAsync_ShouldSubtractFromOldAndAddToNew()
    {
        (Guid userId, Guid oldCategoryId) = await CreateUserAndCategoryAsync();

        Guid newCategoryId = Guid.NewGuid();
        await Context.Categories.AddAsync(entity: new CategoryEntity()
        {
            Id = newCategoryId,
            UserId = userId,
            ParentId = null,
            Name = "Транспорт",
            Type = CategoryType.Expense,
            IsArchived = false,
            CreatedAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync();

        DateTime occurredAt = new DateTime(
            year: 2025,
            month: 1,
            day: 15,
            hour: 0,
            minute: 0,
            second: 0,
            kind: DateTimeKind.Utc);

        await _writeRepository.AddAsync(
            userId: userId,
            categoryId: oldCategoryId,
            amount: 1000m,
            occurredAt: occurredAt
        );
        await _writeRepository.ChangeCategoryAsync(
            userId: userId,
            oldCategoryId: oldCategoryId,
            newCategoryId: newCategoryId,
            amount: 1000m,
            occurredAt: occurredAt
        );

        CategoryTotalEntity oldEntity = await Context.CategoryTotals.FirstAsync(
            predicate: ct => ct.UserId == userId && ct.CategoryId == oldCategoryId
        );
        CategoryTotalEntity newEntity = await Context.CategoryTotals.FirstAsync(
            predicate: ct => ct.UserId == userId && ct.CategoryId == newCategoryId
        );

        await Assert.That(value: oldEntity.Total).IsEqualTo(expected: 0m);
        await Assert.That(value: newEntity.Total).IsEqualTo(expected: 1000m);
    }
}