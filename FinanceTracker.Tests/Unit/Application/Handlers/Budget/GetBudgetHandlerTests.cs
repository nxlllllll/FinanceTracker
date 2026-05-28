using FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class GetBudgetHandlerTests
{
	private IBudgetReadRepository _budgetReadRepository = null!;
	private GetBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetReadRepository = Substitute.For<IBudgetReadRepository>();

		_handler = new GetBudgetHandler(budgetReadRepository: _budgetReadRepository);
	}

	[Test]
	public async Task Handle_WhenBudgetExists_ShouldReturnBudgetDto()
	{
		BudgetReadModel? budget = BudgetFactory.CreateReadModel();

		_budgetReadRepository.GetByIdAsync(
			budgetId: budget.Id,
			userId: budget.UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: budget);

		BudgetReadModel? result = await _handler.Handle(
			query: new GetBudgetQuery(UserId: budget.UserId, BudgetId: budget.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.Id).IsEqualTo(expected: budget.Id);
	}

	[Test]
	public async Task Handle_WhenBudgetNotFound_ShouldReturnNull()
	{
		_budgetReadRepository.GetByIdAsync(
			budgetId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<BudgetReadModel?>(result: null));

		BudgetReadModel? result = await _handler.Handle(
			query: new GetBudgetQuery(UserId: Guid.CreateVersion7(), BudgetId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}
}
