using FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ChangeBudgetAmountHandlerTests
{
    private IBudgetReadRepository _budgetReadRepository = null!;
    private IBudgetWriteRepository _budgetWriteRepository = null!;
    private ChangeBudgetAmountHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _budgetReadRepository = Substitute.For<IBudgetReadRepository>();
        _budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
        
        _handler = new ChangeBudgetAmountHandler(
            budgetReadRepository: _budgetReadRepository,
            budgetWriteRepository: _budgetWriteRepository
        );
    }

    [Test]
    public async Task Handle_WhenBudgetExists_ShouldCallChangeAmountAsync()
    {
        BudgetDto budget = BudgetFactory.Create();

        _budgetReadRepository.GetByIdAsync(
            budgetId: budget.Id,
            userId: budget.UserId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: budget);

        await _handler.Handle(
            command: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: 5000m),
            ct: CancellationToken.None
        );

        await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ChangeAmountAsync(
            budgetId: budget.Id,
            amount: 5000m,
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
            command: new ChangeBudgetAmountCommand(UserId: Guid.NewGuid(), BudgetId: Guid.NewGuid(), Amount: 5000m),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }
}