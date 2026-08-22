using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgets;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.ReadModels.Budget;
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

	private static PagedResult<BudgetReadModel> EmptyPage()
	{
		return new PagedResult<BudgetReadModel>(
			Items: [],
			HasNextPage: false,
			NextCursorDate: null,
			NextCursorId: null
		);
	}

	private static PagedResult<BudgetReadModel> PageOf(IReadOnlyList<BudgetReadModel> items)
	{
		return new PagedResult<BudgetReadModel>(
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
		IReadOnlyList<BudgetReadModel> budgets = [
			BudgetFactory.CreateReadModel(userId: userId),
			BudgetFactory.CreateReadModel(userId: userId)
		];

		_budgetReadRepository.GetAllAsync(
			userId: Arg.Any<Guid>(),
			cursorCreatedAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			pageSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: PageOf(items: budgets));

		Result<PagedResult<BudgetReadModel>, AppException> result = await _handler.Handle(
			query: new GetBudgetsQuery(UserId: userId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Items.Count).IsEqualTo(expected: 2);
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

		Result<PagedResult<BudgetReadModel>, AppException> result = await _handler.Handle(
			query: new GetBudgetsQuery(UserId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value?.Items).IsEmpty();
	}
}
