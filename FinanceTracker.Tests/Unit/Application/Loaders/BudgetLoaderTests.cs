using FinanceTracker.Application.UseCases.Budgets.Authorization;
using FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class BudgetLoaderTests
{
	private IBudgetReadRepository _budgetReadRepository = null!;
	private BudgetLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetReadRepository = Substitute.For<IBudgetReadRepository>();
		_loader = new BudgetLoader(budgetReadRepository: _budgetReadRepository);
	}

	[Test]
	public async Task LoadAsync_WhenNotFound_ShouldThrowNotFoundException()
	{
		_budgetReadRepository.GetByIdAsync(
			budgetId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Budget?>(result: null));

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ChangeBudgetAmountCommand(UserId: Guid.NewGuid(), BudgetId: Guid.NewGuid(), Amount: 1000m),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnBudget()
	{
		Budget budget = BudgetFactory.Create().Value!;
		_budgetReadRepository.GetByIdAsync(
			budgetId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: budget);

		Budget result = await _loader.LoadAsync(
			request: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: 1000m),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: budget.Id);
	}
}