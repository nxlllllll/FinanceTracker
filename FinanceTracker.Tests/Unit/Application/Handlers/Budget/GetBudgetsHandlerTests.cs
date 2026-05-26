using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgets;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
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

	private static PagedResult<FinanceTracker.Core.Domains.Budget.Budget> EmptyPage()
	{
		return new PagedResult<FinanceTracker.Core.Domains.Budget.Budget>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<FinanceTracker.Core.Domains.Budget.Budget> PageOf(IReadOnlyList<FinanceTracker.Core.Domains.Budget.Budget> items)
	{
		return new PagedResult<FinanceTracker.Core.Domains.Budget.Budget>(
			Items: items,
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	[Test]
	public async Task Handle_ShouldReturnAllBudgets()
	{
		Guid userId = Guid.CreateVersion7();
		IReadOnlyList<FinanceTracker.Core.Domains.Budget.Budget> budgets = [
			BudgetFactory.Create(userId: userId).Value!,
			BudgetFactory.Create(userId: userId).Value!
		];

		_budgetReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: budgets));

		PagedResult<FinanceTracker.Core.Domains.Budget.Budget> result = await _handler.Handle(
			query: new GetBudgetsQuery(UserId: userId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Handle_WhenNoBudgets_ShouldReturnEmptyList()
	{
		_budgetReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: EmptyPage());

		PagedResult<FinanceTracker.Core.Domains.Budget.Budget> result = await _handler.Handle(
			query: new GetBudgetsQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Items).IsEmpty();
	}
}
