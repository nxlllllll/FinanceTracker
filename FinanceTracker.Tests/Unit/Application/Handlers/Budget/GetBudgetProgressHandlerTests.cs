using FinanceTracker.Application.UseCases.Budget.Queries.GetBudgetProgress;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class GetBudgetProgressHandlerTests
{
	private IBudgetProgressReadRepository _budgetProgressReadRepository = null!;
	private GetBudgetProgressHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetProgressReadRepository = Substitute.For<IBudgetProgressReadRepository>();
		_handler = new GetBudgetProgressHandler(budgetProgressReadRepository: _budgetProgressReadRepository);
	}

	[Test]
	public async Task Handle_WhenProgressExists_ShouldReturnSuccess()
	{
		BudgetProgress model = BudgetFactory.CreateProgress();
		GetBudgetProgressQuery query = new GetBudgetProgressQuery(
			BudgetId: model.BudgetId,
			UserId: Guid.CreateVersion7()
		);

		_budgetProgressReadRepository
			.GetByBudgetIdAsync(budgetId: model.BudgetId, userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: model);

		Result<BudgetProgress, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: model);
	}

	[Test]
	public async Task Handle_WhenProgressNotFound_ShouldReturnNotFound()
	{
		Guid budgetId = Guid.CreateVersion7();
		GetBudgetProgressQuery query = new GetBudgetProgressQuery(
			BudgetId: budgetId,
			UserId: Guid.CreateVersion7()
		);

		_budgetProgressReadRepository
			.GetByBudgetIdAsync(budgetId: budgetId, userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: (BudgetProgress?)null);

		Result<BudgetProgress, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}
