using FinanceTracker.Application.Budgets.Commands.DeleteBudget;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class DeleteBudgetHandlerTests
{
    private IBudgetReadRepository _budgetReadRepository = null!;
    private IBudgetWriteRepository _budgetWriteRepository = null!;
    private DeleteBudgetHandler _handler = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _budgetReadRepository = Substitute.For<IBudgetReadRepository>();
        _budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();

        _handler = new DeleteBudgetHandler(
            budgetReadRepository: _budgetReadRepository,
            budgetWriteRepository: _budgetWriteRepository
        );
    }

    [Test]
    public async Task Handle_WhenBudgetExists_ShouldCallDeleteAsync()
    {
        BudgetDto budget = BudgetFactory.Create();

        _budgetReadRepository.GetByIdAsync(
            budgetId: budget.Id,
            userId: budget.UserId,
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: budget);

        await _handler.Handle(
            command: new DeleteBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
            ct: CancellationToken.None
        );

        await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
            budgetId: budget.Id,
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
            command: new DeleteBudgetCommand(UserId: Guid.NewGuid(), BudgetId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenBudgetNotFound_ShouldNotCallDeleteAsync()
    {
        _budgetReadRepository.GetByIdAsync(
            budgetId: Arg.Any<Guid>(),
            userId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: Task.FromResult<BudgetDto?>(result: null));

        await Assert.That(action: async () => await _handler.Handle(
            command: new DeleteBudgetCommand(UserId: Guid.NewGuid(), BudgetId: Guid.NewGuid()),
            ct: CancellationToken.None
        )).Throws<NotFoundException>();

        await _budgetWriteRepository.DidNotReceive().DeleteAsync(
            budgetId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }
}