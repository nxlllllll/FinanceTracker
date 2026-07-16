using FinanceTracker.Application.UseCases.Budget.Queries.GetBudget;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
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
	public async Task Handle_WhenBudgetExists_ShouldReturnSuccess()
	{
		BudgetReadModel model = BudgetFactory.CreateReadModel();
		GetBudgetQuery query = new GetBudgetQuery(
			BudgetId: model.Id,
			UserId: model.UserId
		);

		_budgetReadRepository
			.GetByIdAsync(budgetId: model.Id, userId: model.UserId, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: model);

		Result<BudgetReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: model);
	}

	[Test]
	public async Task Handle_WhenBudgetNotFound_ShouldReturnNotFound()
	{
		Guid budgetId = Guid.CreateVersion7();
		GetBudgetQuery query = new GetBudgetQuery(
			BudgetId: budgetId,
			UserId: Guid.CreateVersion7()
		);

		_budgetReadRepository
			.GetByIdAsync(budgetId: budgetId, userId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: (BudgetReadModel?)null);

		Result<BudgetReadModel, AppException> result = await _handler.Handle(
			query: query,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}
}
