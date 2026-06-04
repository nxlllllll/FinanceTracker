using FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class DeactivateBudgetHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private DeactivateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_handler = new DeactivateBudgetHandler(budgetWriteRepository: _budgetWriteRepository);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsActive_ShouldCallDeactivate()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).DeactivateAsync(
			budgetId: budget.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsActive_ShouldReturnBudgetId()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: budget.Id);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyInactive_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<DeactivatingException>();
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyInactive_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.DidNotReceive().DeactivateAsync(
			budgetId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}