using FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ChangeBudgetAmountHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private ChangeBudgetAmountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_handler = new ChangeBudgetAmountHandler(budgetWriteRepository: _budgetWriteRepository);
	}

	[Test]
	public async Task HandleAsync_ShouldCallChangeAmount()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create();

		await _handler.HandleAsync(
			command: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: 5000m),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ChangeAmountAsync(
			budgetId: budget.Id,
			amount: 5000m,
			ct: Arg.Any<CancellationToken>()
		);
	}
}