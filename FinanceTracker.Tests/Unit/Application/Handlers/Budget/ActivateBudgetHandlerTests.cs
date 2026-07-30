using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Commands.ActivateBudget;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ActivateBudgetHandlerTests
{
	private IBudgetReadRepository _budgetReadRepository = null!;
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ActivateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetReadRepository = Substitute.For<IBudgetReadRepository>();
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<bool>>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<bool>>>()?.Invoke());

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
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetIsInactive_ShouldCallActivate()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		budget.Deactivate();

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
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
			budget: budget,
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

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
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
			budget: budget,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(
			notification: Arg.Is<BudgetActivatedNotification>(predicate: n => n!.BudgetId == budget.Id && n.UserId == budget.UserId)
		);
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyActive_ShouldReturnSuccess()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenBudgetAlreadyActive_ShouldNotCallRepository()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
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
			budget: budget,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<BudgetActivatedNotification>());
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

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ActivateBudgetCommand(UserId: budget.UserId, BudgetId: budget.Id),
			budget: budget,
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
			budget: budget,
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
			budget: budget,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<BudgetActivatedNotification>());
	}
}
