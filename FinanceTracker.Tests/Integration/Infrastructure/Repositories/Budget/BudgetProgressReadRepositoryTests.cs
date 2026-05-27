using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Budget;

public sealed class BudgetProgressReadRepositoryTests : DatabaseFixture
{
    private BudgetProgressReadRepository _readRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private UserBuilder _userBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;
    private BudgetBuilder _budgetBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _readRepository = new BudgetProgressReadRepository(context: Context);
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _userBuilder = new UserBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
        _budgetBuilder = new BudgetBuilder(context: Context);
        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            onError: Arg.Any<Func<Exception, Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
    }

    [Test]
    public async Task GetByBudgetIdAsync_WhenExists_ShouldReturnCorrectDto()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId, amount: 10000m);

        await Context.BudgetProgresses.Where(predicate: p => p.BudgetId == budgetId)
            .ExecuteUpdateAsync(setPropertyCalls: builder => builder
                .SetProperty(propertyExpression: p => p.Spent, valueExpression: 3000m)
                .SetProperty(propertyExpression: p => p.UpdatedAt, valueExpression: DateTimeOffset.UtcNow)
            );

        BudgetProgress? result = await _readRepository.GetByBudgetIdAsync(budgetId: budgetId, userId: userId);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.BudgetId).IsEqualTo(expected: budgetId);
        await Assert.That(value: result.Spent).IsEqualTo(expected: 3000m);
        await Assert.That(value: result.Remaining).IsEqualTo(expected: 7000m);
        await Assert.That(value: result.Percentage).IsEqualTo(expected: 0.3m);
    }

    [Test]
    public async Task GetByBudgetIdAsync_WhenNotExists_ShouldReturnNull()
    {
        BudgetProgress? result = await _readRepository.GetByBudgetIdAsync(budgetId: Guid.CreateVersion7(), userId: Guid.CreateVersion7());

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByBudgetIdAsync_WhenSpentIsZero_ShouldReturnZeroPercentage()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

        BudgetProgress? result = await _readRepository.GetByBudgetIdAsync(budgetId: budgetId, userId: userId);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Spent).IsEqualTo(expected: 0m);
        await Assert.That(value: result.Remaining).IsEqualTo(expected: 10000m);
        await Assert.That(value: result.Percentage).IsEqualTo(expected: 0m);
    }
}
