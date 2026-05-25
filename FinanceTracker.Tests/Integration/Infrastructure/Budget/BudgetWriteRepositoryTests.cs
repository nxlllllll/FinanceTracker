using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.Budget;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Budget;

public sealed class BudgetWriteRepositoryTests : DatabaseFixture
{
    private BudgetWriteRepository _writeRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private UserBuilder _userBuilder = null!;
    private CategoryBuilder _categoryBuilder = null!;

    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _writeRepository = new BudgetWriteRepository(
            context: Context,
            dateProvider: FakeDateProvider.Default,
            unitOfWork: _unitOfWork,
            logger: Substitute.For<ILogger<BudgetWriteRepository>>()
        );
        _unitOfWork.ExecuteInTransactionAsync(
            operation: Arg.Any<Func<Task>>(),
            onError: Arg.Any<Func<Exception, Task>>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: callInfo => callInfo.ArgAt<Func<Task>>(position: 0)());
        _userBuilder = new UserBuilder(context: Context);
        _categoryBuilder = new CategoryBuilder(context: Context);
    }

   [Test]
   public async Task CreateAsync_ShouldCreateBudgetAndProgress()
   {
       Guid userId = await _userBuilder.CreateAsync();
       Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);
   
       Result<Core.Domains.Budget.Budget, DomainException> result = Core.Domains.Budget.Budget.Create(
           createdAt: FakeDateProvider.Default.UtcNow,
           userId: userId,
           categoryId: categoryId,
           amount: Money.Create(amount: 10000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
           from: new DateOnly(year: 2025, month: 1, day: 1),
           to: new DateOnly(year: 2025, month: 1, day: 31)
       );
   
       Core.Domains.Budget.Budget budget = result.Value!;
       
       await _writeRepository.CreateAsync(budget: budget);
   
       BudgetEntity? budgetEntity = await Context.Budgets.FirstOrDefaultAsync(predicate: b => b.Id == budget.Id);
       BudgetProgressEntity? progress = await Context.BudgetProgresses.FirstOrDefaultAsync(predicate: p => p.BudgetId == budget.Id);
   
       await Assert.That(value: budgetEntity).IsNotNull();
       await Assert.That(value: budgetEntity!.Amount).IsEqualTo(expected: 10000m);
       await Assert.That(value: budgetEntity.Currency.Value).IsEqualTo(expected: "RUB");
       await Assert.That(value: progress).IsNotNull();
       await Assert.That(value: progress!.Spent).IsEqualTo(expected: 0m);
   }

    [Test]
    public async Task ChangePeriodAsync_ShouldUpdatePeriod()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        Result<Core.Domains.Budget.Budget, DomainException> b = Core.Domains.Budget.Budget.Create(
            createdAt: FakeDateProvider.Default.UtcNow,
            userId: userId,
            categoryId: categoryId,
            amount: Money.Create(amount: 10000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 31)
        );

        Core.Domains.Budget.Budget budget = b.Value!;
        await _writeRepository.CreateAsync(budget: budget);

        await _writeRepository.ChangePeriodAsync(
            budgetId: budget.Id,
            from: new DateOnly(year: 2025, month: 2, day: 1),
            to: new DateOnly(year: 2025, month: 2, day: 28)
        );

        BudgetEntity result = await Context.Budgets.AsNoTracking().FirstAsync(predicate: b => b.Id == budget.Id);

        await Assert.That(value: result.From).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 1));
        await Assert.That(value: result.To).IsEqualTo(expected: new DateOnly(year: 2025, month: 2, day: 28));
    }

    [Test]
    public async Task DeleteAsync_ShouldDeleteBudgetAndProgress()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid categoryId = await _categoryBuilder.CreateAsync(userId: userId);

        Result<Core.Domains.Budget.Budget, DomainException> budgetResult = Core.Domains.Budget.Budget.Create(
            createdAt: FakeDateProvider.Default.UtcNow,
           userId: userId,
            categoryId: categoryId,
            amount: Money.Create(amount: 10000m, currency: Core.ValueObjects.Currency.Create(value: "RUB").Value).Value,
            from: new DateOnly(year: 2025, month: 1, day: 1),
            to: new DateOnly(year: 2025, month: 1, day: 31)
        );
        Core.Domains.Budget.Budget budget = budgetResult.Value!;
        
        await _writeRepository.CreateAsync(budget: budget);

        await _writeRepository.DeleteAsync(budgetId: budget.Id);

        BudgetEntity? result = await Context.Budgets.FirstOrDefaultAsync(predicate: b => b.Id == budget.Id);
        BudgetProgressEntity? progress = await Context.BudgetProgresses.FirstOrDefaultAsync(predicate: p => p.BudgetId == budget.Id);

        await Assert.That(value: result).IsNull();
        await Assert.That(value: progress).IsNull();
    }
}
