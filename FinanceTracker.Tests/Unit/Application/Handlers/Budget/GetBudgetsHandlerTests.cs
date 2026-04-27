using FinanceTracker.Application.Budgets.Queries.GetBudgets;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class GetBudgetsHandlerTests
{
	private IBudgetReadRepository _budgetReadRepository = null!;
	private GetBudgetsHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetReadRepository = Substitute.For<IBudgetReadRepository>();
		_handler = new GetBudgetsHandler(budgetReadRepository: _budgetReadRepository);
	}

	[Test]
	public async Task Handle_ShouldReturnAllBudgets()
	{
		Guid userId = Guid.NewGuid();
		IReadOnlyList<BudgetDto> budgets = [BudgetFactory.Create(userId: userId), BudgetFactory.Create(userId: userId)];

		_budgetReadRepository.GetAllAsync(
			userId: userId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: budgets);

		IReadOnlyList<BudgetDto> result = await _handler.Handle(
			query: new GetBudgetsQuery(UserId: userId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Handle_WhenNoBudgets_ShouldReturnEmptyList()
	{
		_budgetReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		IReadOnlyList<BudgetDto> result = await _handler.Handle(
			query: new GetBudgetsQuery(UserId: Guid.NewGuid()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsEmpty();
	}
}