using FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ActivateBudgetHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private ActivateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_handler = new ActivateBudgetHandler(budgetWriteRepository: _budgetWriteRepository);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsInactive_ShouldCallActivate()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ActivateAsync(
			budgetId: budget.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsInactive_ShouldReturnBudgetId()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: budget.Id);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyActive_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ActivatingException>();
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyActive_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.DidNotReceive().ActivateAsync(
			budgetId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}