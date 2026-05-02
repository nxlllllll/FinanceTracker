using FinanceTracker.Application.Budgets.Commands.ChangeBudgetPeriod;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ChangeBudgetPeriodHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private ChangeBudgetPeriodHandler _handler = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		
		_handler = new ChangeBudgetPeriodHandler(
			budgetWriteRepository: _budgetWriteRepository,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			unitOfWork: _unitOfWork
		);
	}

	[Test]
	public async Task HandleAsync_ShouldCallChangePeriod()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create();
		DateOnly from = new DateOnly(year: 2025, month: 2, day: 1);
		DateOnly to = new DateOnly(year: 2025, month: 2, day: 28);

		await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: from, To: to),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ChangePeriodAsync(
			budgetId: budget.Id,
			dateFrom: from,
			dateTo: to,
			ct: Arg.Any<CancellationToken>()
		);
	}
}