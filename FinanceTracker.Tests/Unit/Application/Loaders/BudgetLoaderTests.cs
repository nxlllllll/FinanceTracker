using FinanceTracker.Application.Budgets.Authorization;
using FinanceTracker.Application.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
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
		).Returns(returnThis: Task.FromResult<BudgetDto?>(result: null));

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ChangeBudgetAmountCommand(UserId: Guid.NewGuid(), BudgetId: Guid.NewGuid(), Amount: 1000m),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnBudget()
	{
		BudgetDto budget = BudgetFactory.Create();
		_budgetReadRepository.GetByIdAsync(
			budgetId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: budget);

		BudgetDto result = await _loader.LoadAsync(
			request: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: 1000m),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: budget.Id);
	}
}