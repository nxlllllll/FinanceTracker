using FinanceTracker.Core.Persistence;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Budget;

public sealed class BudgetReadRepositoryTests : DatabaseFixture
{
    private BudgetReadRepository _readRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private UserBuilder _userBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;
    private BudgetBuilder _budgetBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _readRepository = new BudgetReadRepository(context: Context);
        _userBuilder = new UserBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _budgetBuilder = new BudgetBuilder(
            context: Context,
            unitOfWork: _unitOfWork,
            logger: Substitute.For<ILogger<BudgetWriteRepository>>()
        );
        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            onError: Arg.Any<Func<Exception, Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
    }

    [Test]
    public async Task GetByIdAsync_WhenExists_ShouldReturnBudgetDto()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

        Core.Domains.Budget.Budget? result = await _readRepository.GetByIdAsync(budgetId: budgetId, userId: userId);

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Id).IsEqualTo(expected: budgetId);
        await Assert.That(value: result.Amount.Amount).IsEqualTo(expected: 10000m);
    }

    [Test]
    public async Task GetByIdAsync_WhenNotExists_ShouldReturnNull()
    {
        Core.Domains.Budget.Budget? result = await _readRepository.GetByIdAsync(
            budgetId: Guid.CreateVersion7(),
            userId: Guid.CreateVersion7()
        );

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetByIdAsync_ShouldNotReturnOtherUserBudget()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

        Core.Domains.Budget.Budget? result = await _readRepository.GetByIdAsync(
            budgetId: budgetId,
            userId: Guid.CreateVersion7()
        );

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetActiveByCategoryAsync_WhenDateInPeriod_ShouldReturnBudget()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        Guid budgetId = await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

        Core.Domains.Budget.Budget? result = await _readRepository.GetActiveByCategoryAsync(
            userId: userId,
            categoryId: categoryId,
            date: new DateOnly(year: 2025, month: 1, day: 15)
        );

        await Assert.That(value: result).IsNotNull();
        await Assert.That(value: result!.Id).IsEqualTo(expected: budgetId);
    }

    [Test]
    public async Task GetActiveByCategoryAsync_WhenDateOutOfPeriod_ShouldReturnNull()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
        await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId);

        Core.Domains.Budget.Budget? result = await _readRepository.GetActiveByCategoryAsync(
            userId: userId,
            categoryId: categoryId,
            date: new DateOnly(2025, 2, 1)
        );

        await Assert.That(value: result).IsNull();
    }

    [Test]
    public async Task GetAllAsync_ShouldReturnAllUserBudgets()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId1 = await _categoryBuilder.CreateAsync(userId: userId, name: "Еда");
        Guid categoryId2 = await _categoryBuilder.CreateAsync(userId: userId, name: "Транспорт");
        await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId1);
        await _budgetBuilder.CreateAsync(userId: userId, categoryId: categoryId2);

        IReadOnlyList<Core.Domains.Budget.Budget> result = await _readRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result.Count).IsEqualTo(expected: 2);
    }

    [Test]
    public async Task GetAllAsync_ShouldNotReturnOtherUserBudgets()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid anotherUserId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: anotherUserId);
        await _budgetBuilder.CreateAsync(userId: anotherUserId, categoryId: categoryId);

        IReadOnlyList<Core.Domains.Budget.Budget> result = await _readRepository.GetAllAsync(userId: userId);

        await Assert.That(value: result).IsEmpty();
    }
}