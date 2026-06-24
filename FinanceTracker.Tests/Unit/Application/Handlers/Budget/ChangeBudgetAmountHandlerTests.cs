using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetAmount;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ChangeBudgetAmountHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IPublisher _publisher = null!;
	private ChangeBudgetAmountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new ChangeBudgetAmountHandler(
			budgetWriteRepository: _budgetWriteRepository,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_ShouldCallChangeAmount()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: 5000m),
			accounts: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ChangeAmountAsync(
			budgetId: budget.Id,
			amount: 5000m,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: 5000m),
			accounts: budget,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<BudgetAmountChangedNotification>(n =>
				n.BudgetId == budget.Id &&
				n.UserId == budget.UserId &&
				n.NewAmount == 5000m),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsInvalid_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeBudgetAmountCommand(UserId: budget.UserId, BudgetId: budget.Id, Amount: -1m),
			accounts: budget,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<BudgetAmountChangedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}