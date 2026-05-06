using FinanceTracker.Application.UseCases.Budgets.Authorization;
using FinanceTracker.Application.UseCases.Budgets.Commands.ChangeBudgetAmount;
using FinanceTracker.Core.Domains.Budget;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
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

		Result<Budget, NotFoundException> result = await _loader.LoadAsync(
			request: new ChangeBudgetAmountCommand(UserId: Guid.NewGuid(), BudgetId: Guid.NewGuid(), Amount: 1000m),
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
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

		Result<Budget, NotFoundException> result = await _loader.LoadAsync(
			request: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: 1000m),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: budget.Id);
	}
}