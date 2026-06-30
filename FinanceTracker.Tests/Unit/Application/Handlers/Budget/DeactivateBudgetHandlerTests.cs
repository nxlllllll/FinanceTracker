using FinanceTracker.Application.UseCases.Budget.Commands.DeactivateBudget;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class DeactivateBudgetHandlerTests
{
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IPublisher _publisher = null!;
	private DeactivateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new DeactivateBudgetHandler(
			budgetWriteRepository: _budgetWriteRepository,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<DeactivateBudgetHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsActive_ShouldCallDeactivate()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
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
			entity: budget,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<BudgetDeactivatedNotification>(n => n.BudgetId == budget.Id && n.UserId == budget.UserId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyInactive_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<DeactivatingException>();
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyInactive_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new DeactivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<BudgetDeactivatedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}