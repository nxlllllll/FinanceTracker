using FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ChangeBudgetPeriodHandlerTests
{
    private IBudgetReadRepository _budgetReadRepository = null!;
    private IBudgetWriteRepository _budgetWriteRepository = null!;
    private ChangeBudgetPeriodHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _budgetReadRepository = Substitute.For<IBudgetReadRepository>();
        _budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();

        _handler = new ChangeBudgetPeriodHandler(
            budgetReadRepository:  _budgetReadRepository,
            budgetWriteRepository: _budgetWriteRepository
        );
    }

    [Test]
    public async Task Handle_WhenBudgetExists_ShouldCallChangePeriodAsync()
    {
        BudgetDto budget = BudgetFactory.Create();

        _budgetReadRepository.GetByIdAsync(
            budgetId: budget.Id,
            userId: budget.UserId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: budget);

        await _handler.Handle(
            command: new ChangeBudgetPeriodCommand(
                UserId: budget.UserId,
                BudgetId: budget.Id,
                From: new DateOnly(year: 2025, month: 2, day: 1),
                To: new DateOnly(year: 2025, month: 2, day: 28)
            ),
            ct: CancellationToken.None
        );

        await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ChangePeriodAsync(
            budgetId: budget.Id,
            dateFrom: new DateOnly(year: 2025, month: 2, day: 1),
            dateTo: new DateOnly(year: 2025, month: 2, day: 28),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WhenBudgetNotFound_ShouldThrowNotFoundException()
    {
        _budgetReadRepository.GetByIdAsync(
            budgetId: Arg.Any<Guid>(),
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<BudgetDto?>(result: null));

        await Assert.That(action: async () => await _handler.Handle(
            command: new ChangeBudgetPeriodCommand(
                UserId: Guid.NewGuid(),
                BudgetId: Guid.NewGuid(),
                From: new DateOnly(year: 2025, month: 2, day: 1),
                To: new DateOnly(year: 2025, month: 2, day: 28)
            ),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }
}