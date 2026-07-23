using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class DeactivateBudgetHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private DeactivateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();
		_handler = new DeactivateBudgetHandler(
			budgetWriteRepository: _budgetWriteRepository,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsActive_ShouldCallDeactivate()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			user: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).DeactivateAsync(
			budgetId: budget.Id,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsActive_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			user: budget,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Is<BudgetDeactivatedNotification>(n => n!.BudgetId == budget.Id && n.UserId == budget.UserId
		));
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyInactive_ShouldReturnSuccess()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			user: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyInactive_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			user: budget,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<BudgetDeactivatedNotification>());
	}
}
