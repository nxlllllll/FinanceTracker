using FinanceTracker.Application.UseCases.Budgets.Queries.GetBudgetProgress;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Repositories.BudgetProgress;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class GetBudgetProgressHandlerTests
{
	private IBudgetProgressReadRepository _budgetProgressReadRepository = null!;
	private GetBudgetProgressHandler _handler = null!;

	private static readonly Guid UserId = Guid.CreateVersion7();

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetProgressReadRepository = Substitute.For<IBudgetProgressReadRepository>();
		_handler = new GetBudgetProgressHandler(budgetProgressReadRepository: _budgetProgressReadRepository);
	}

	[Test]
	public async Task Handle_WhenProgressExists_ShouldReturnBudgetProgressDto()
	{
		Guid budgetId = Guid.CreateVersion7();
		BudgetProgressDto progress = BudgetFactory.CreateProgress(budgetId: budgetId, spent: 3000m);

		_budgetProgressReadRepository.GetByBudgetIdAsync(
			budgetId: budgetId,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: progress);

		BudgetProgressDto? result = await _handler.Handle(
			query: new GetBudgetProgressQuery(BudgetId: budgetId, UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNotNull();
		await Assert.That(value: result!.BudgetId).IsEqualTo(expected: budgetId);
		await Assert.That(value: result.Spent).IsEqualTo(expected: 3000m);
		await Assert.That(value: result.Remaining).IsEqualTo(expected: 7000m);
	}

	[Test]
	public async Task Handle_WhenProgressNotFound_ShouldReturnNull()
	{
		_budgetProgressReadRepository.GetByBudgetIdAsync(
			budgetId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<BudgetProgressDto?>(null));

		BudgetProgressDto? result = await _handler.Handle(
			query: new GetBudgetProgressQuery(BudgetId: Guid.CreateVersion7(), UserId: UserId),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task Handle_ShouldPassBothBudgetIdAndUserIdToRepository()
	{
		Guid budgetId = Guid.CreateVersion7();

		_budgetProgressReadRepository.GetByBudgetIdAsync(
			budgetId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<BudgetProgressDto?>(null));

		await _handler.Handle(
			query: new GetBudgetProgressQuery(BudgetId: budgetId, UserId: UserId),
			ct: CancellationToken.None
		);

		await _budgetProgressReadRepository.Received(requiredNumberOfCalls: 1).GetByBudgetIdAsync(
			budgetId: budgetId,
			userId: UserId,
			ct: Arg.Any<CancellationToken>()
		);
	}
}
