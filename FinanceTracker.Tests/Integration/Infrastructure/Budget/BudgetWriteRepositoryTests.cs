using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Budget;

public sealed class BudgetWriteRepositoryTests : DatabaseFixture
{
    private BudgetWriteRepository _writeRepository = null!;
    private UserBuilder _userBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _writeRepository = new BudgetWriteRepository(context: Context);
        _userBuilder = new UserBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
    }

    [Test]
    public async Task CreateAsync_ShouldCreateBudgetAndProgress()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            budgetId: budgetId,
            userId: userId,
            categoryId: categoryId,
            currency: "RUB",
            amount: 10000m,
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 31)
        );

        BudgetEntity? budget = await Context.Budgets.FirstOrDefaultAsync(predicate: b => b.Id == budgetId);
        BudgetProgressEntity? progress = await Context.BudgetProgresses.FirstOrDefaultAsync(predicate: p => p.BudgetId == budgetId);

        await Assert.That(value: budget).IsNotNull();
        await Assert.That(value: budget!.Amount).IsEqualTo(expected: 10000m);
        await Assert.That(value: budget.Currency).IsEqualTo(expected: "RUB");
        await Assert.That(value: progress).IsNotNull();
        await Assert.That(value: progress!.Spent).IsEqualTo(expected: 0m);
    }

    [Test]
    public async Task ChangeAmountAsync_ShouldUpdateAmount()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            budgetId: budgetId,
            userId: userId,
            categoryId: categoryId,
            currency: "RUB",
            amount: 10000m,
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 31)
        );

        await _writeRepository.ChangeAmountAsync(budgetId: budgetId, amount: 20000m);

        BudgetEntity budget = await Context.Budgets.AsNoTracking().FirstAsync(predicate: b => b.Id == budgetId);

        await Assert.That(value: budget.Amount).IsEqualTo(expected: 20000m);
    }

    [Test]
    public async Task ChangePeriodAsync_ShouldUpdatePeriod()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            budgetId: budgetId,
            userId: userId,
            categoryId: categoryId,
            currency: "RUB",
            amount: 10000m,
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 31)
        );

        await _writeRepository.ChangePeriodAsync(
            budgetId: budgetId,
            from: new DateOnly(year: 2025, month: 2, day: 1),
            to: new DateOnly(year: 2025, month: 2, day: 28)
        );

        BudgetEntity budget = await Context.Budgets.AsNoTracking().FirstAsync(predicate: b => b.Id == budgetId);

        await Assert.That(value: budget.From).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 1));
        await Assert.That(value: budget.To).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 28));
    }

    [Test]
    public async Task DeleteAsync_ShouldDeleteBudgetAndProgress()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = Guid.NewGuid();

        await _writeRepository.CreateAsync(
            budgetId: budgetId,
            userId: userId,
            categoryId: categoryId,
            currency: "RUB",
            amount: 10000m,
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 31)
        );

        await _writeRepository.DeleteAsync(budgetId: budgetId);

        BudgetEntity? budget = await Context.Budgets.FirstOrDefaultAsync(predicate: b => b.Id == budgetId);
        BudgetProgressEntity? progress = await Context.BudgetProgresses.FirstOrDefaultAsync(predicate: p => p.BudgetId == budgetId);

        await Assert.That(value: budget).IsNull();
        await Assert.That(value: progress).IsNull();
    }
}