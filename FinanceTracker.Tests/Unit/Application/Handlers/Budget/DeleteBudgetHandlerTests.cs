using FinanceTracker.Application.Budgets.Commands.DeleteBudget;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class DeleteBudgetHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private DeleteBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_handler = new DeleteBudgetHandler(budgetWriteRepository: _budgetWriteRepository);
	}

	[Test]
	public async Task HandleAsync_ShouldCallDelete()
	{
		BudgetDto budget = BudgetFactory.Create();

		await _handler.HandleAsync(
			command: new DeleteBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).DeleteAsync(
			budgetId: budget.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}
}