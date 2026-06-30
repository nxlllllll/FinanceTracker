using FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ActivateBudgetHandlerTests
{
	private IBudgetReadRepository _budgetReadRepository = null!;
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPublisher _publisher = null!;
	private ActivateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetReadRepository = Substitute.For<IBudgetReadRepository>();
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_publisher = Substitute.For<IPublisher>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<bool>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<bool>>>()());

		_budgetReadRepository.HasOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		_handler = new ActivateBudgetHandler(
			budgetReadRepository: _budgetReadRepository,
			budgetWriteRepository: _budgetWriteRepository,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ActivateBudgetHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsInactive_ShouldCallActivate()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ActivateAsync(
			budgetId: budget.Id,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsInactive_ShouldCheckForOverlapExcludingItself()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _budgetReadRepository.Received(requiredNumberOfCalls: 1).HasOverlappingAsync(
			userId: budget.UserId,
			categoryId: budget.CategoryId,
			from: budget.From,
			to: budget.To,
			excludeBudgetId: budget.Id,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsInactive_ShouldReturnBudgetId()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsEqualTo(expected: budget.Id);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsInactive_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<BudgetActivatedNotification>(n => n.BudgetId == budget.Id && n.UserId == budget.UserId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyActive_ShouldReturnFailure()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ActivatingException>();
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyActive_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.DidNotReceive().ActivateAsync(
			budgetId: Arg.Any<Guid>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyActive_ShouldNotPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<BudgetActivatedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldReturnFailure()
	{
		_budgetReadRepository.HasOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<OverlappingBudgetException>();
	}

	[Test]
	public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldNotCallActivateAsync()
	{
		_budgetReadRepository.HasOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.DidNotReceive().ActivateAsync(
			budgetId: Arg.Any<Guid>(),
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldNotPublishNotification()
	{
		_budgetReadRepository.HasOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: true);

		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			entity: budget,
			ct: CancellationToken.None
		);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<BudgetActivatedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}