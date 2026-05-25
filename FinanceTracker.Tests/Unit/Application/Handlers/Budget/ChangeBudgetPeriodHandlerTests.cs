using FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ChangeBudgetPeriodHandlerTests
{
    private IBudgetReadRepository _budgetReadRepository = null!;
    private IBudgetWriteRepository _budgetWriteRepository = null!;
    private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
    private IUnitOfWork _unitOfWork = null!;
    private ChangeBudgetPeriodHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _budgetReadRepository = Substitute.For<IBudgetReadRepository>();
        _budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
        _budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
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

        _budgetReadRepository.HasOverlappingAsync(
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            from: Arg.Any<DateOnly>(),
            to: Arg.Any<DateOnly>(),
            excludeBudgetId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: false);

        _handler = new ChangeBudgetPeriodHandler(
            budgetReadRepository: _budgetReadRepository,
            budgetWriteRepository: _budgetWriteRepository,
            budgetProgressWriteRepository: _budgetProgressWriteRepository,
            unitOfWork: _unitOfWork,
            logger: Substitute.For<ILogger<ChangeBudgetPeriodHandler>>()
        );
    }

    [Test]
    public async Task HandleAsync_ShouldCallChangePeriod()
    {
        FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
        DateOnly from = new DateOnly(year: 2025, month: 2, day: 1);
        DateOnly to = new DateOnly(year: 2025, month: 2, day: 28);

        await _handler.HandleAsync(
            command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: from, To: to),
            budget: budget,
            ct: CancellationToken.None
        );

        await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ChangePeriodAsync(
            budgetId: budget.Id,
            from: from,
            to: to,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldReturnFailure()
    {
        _budgetReadRepository.HasOverlappingAsync(
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            from: Arg.Any<DateOnly>(),
            to: Arg.Any<DateOnly>(),
            excludeBudgetId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: true);

        FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

        Result<Guid, DomainException> result = await _handler.HandleAsync(command: new ChangeBudgetPeriodCommand(
            UserId: budget.UserId,
            BudgetId: budget.Id,
            From: new DateOnly(year: 2025, month: 2, day: 1),
            To: new DateOnly(year: 2025, month: 2, day: 28)
        ), budget: budget, ct: CancellationToken.None);

        await Assert.That(value: result.IsFailure).IsTrue();
        await Assert.That(value: result.Error).IsTypeOf<OverlappingBudgetException>();
    }

    [Test]
    public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldNotCallChangePeriodAsync()
    {
        _budgetReadRepository.HasOverlappingAsync(
            userId: Arg.Any<Guid>(),
            categoryId: Arg.Any<Guid>(),
            from: Arg.Any<DateOnly>(),
            to: Arg.Any<DateOnly>(),
            excludeBudgetId: Arg.Any<Guid?>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: true);

        FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

        await _handler.HandleAsync(command: new ChangeBudgetPeriodCommand(
            UserId: budget.UserId,
            BudgetId: budget.Id,
            From: new DateOnly(year: 2025, month: 2, day: 1),
            To: new DateOnly(year: 2025, month: 2, day: 28)
        ), budget: budget, ct: CancellationToken.None);

        await _budgetWriteRepository.DidNotReceive().ChangePeriodAsync(
            budgetId: Arg.Any<Guid>(),
            from: Arg.Any<DateOnly>(),
            to: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task HandleAsync_ShouldExcludeCurrentBudgetFromOverlapCheck()
    {
        FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
        DateOnly from = new DateOnly(year: 2025, month: 2, day: 1);
        DateOnly to = new DateOnly(year: 2025, month: 2, day: 28);

        await _handler.HandleAsync(
            command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: from, To: to),
            budget: budget,
            ct: CancellationToken.None
        );

        await _budgetReadRepository.Received(requiredNumberOfCalls: 1).HasOverlappingAsync(
            userId: budget.UserId,
            categoryId: budget.CategoryId,
            from: from,
            to: to,
            excludeBudgetId: budget.Id,
            ct: Arg.Any<CancellationToken>()
        );
    }
}
