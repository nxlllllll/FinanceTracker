using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Commands.ChangeBudgetPeriod;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Budget;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class ChangeBudgetPeriodHandlerTests
{
	private static readonly Guid ConflictingBudgetId = Guid.CreateVersion7();

	private IBudgetReadRepository _budgetReadRepository = null!;
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IBudgetProgressWriteRepository _budgetProgressWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private ChangeBudgetPeriodHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetReadRepository = Substitute.For<IBudgetReadRepository>();
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_budgetProgressWriteRepository = Substitute.For<IBudgetProgressWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Guid?>>>(),
			onError: Arg.Any<Func<Exception, Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task<Guid?>>>()?.Invoke());

		_budgetReadRepository.FindOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Guid?)null);

		_handler = new ChangeBudgetPeriodHandler(
			budgetReadRepository: _budgetReadRepository,
			budgetWriteRepository: _budgetWriteRepository,
			budgetProgressWriteRepository: _budgetProgressWriteRepository,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default,
			logger: Substitute.For<ILogger<ChangeBudgetPeriodHandler>>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidPeriod_ShouldCallChangePeriodAsync()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		DateOnly newFrom = new DateOnly(year: 2026, month: 1, day: 1);
		DateOnly newTo = new DateOnly(year: 2026, month: 1, day: 31);

		await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: newFrom, To: newTo),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).ChangePeriodAsync(
			budgetId: budget.Id,
			from: newFrom,
			to: newTo,
			expectedVersion: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidPeriod_ShouldRecalculateProgressForTheNewSpan()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		DateOnly newFrom = new DateOnly(year: 2026, month: 1, day: 1);
		DateOnly newTo = new DateOnly(year: 2026, month: 1, day: 31);

		await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: newFrom, To: newTo),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetProgressWriteRepository.Received(requiredNumberOfCalls: 1).RecalculateForBudgetAsync(
			budgetId: budget.Id,
			userId: budget.UserId,
			categoryId: budget.CategoryId,
			fromDate: newFrom,
			toDate: newTo,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithValidPeriod_ShouldPublishNotification()
	{
		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;
		DateOnly newFrom = new DateOnly(year: 2026, month: 1, day: 1);
		DateOnly newTo = new DateOnly(year: 2026, month: 1, day: 31);

		await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: newFrom, To: newTo),
			budget: budget,
			ct: CancellationToken.None
		);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<BudgetPeriodChangedNotification>(n =>
			n!.BudgetId == budget.Id &&
			n.UserId == budget.UserId &&
			n.NewFrom == newFrom &&
			n.NewTo == newTo
		));
	}

	[Test]
	public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldReturnFailure()
	{
		_budgetReadRepository.FindOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ConflictingBudgetId);

		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: DateOnly.MinValue, To: DateOnly.MaxValue),
			budget: budget,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<OverlappingBudgetException>();
	}

	[Test]
	public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldNameTheConflictingBudget()
	{
		_budgetReadRepository.FindOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ConflictingBudgetId);

		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: DateOnly.MinValue, To: DateOnly.MaxValue),
			budget: budget,
			ct: CancellationToken.None
		);

		OverlappingBudgetException error = (OverlappingBudgetException)result.Error!;

		await Assert.That(value: error.ConflictingBudgetId).IsEqualTo(expected: ConflictingBudgetId);
	}

	[Test]
	public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldNotRecalculateProgress()
	{
		_budgetReadRepository.FindOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ConflictingBudgetId);

		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: DateOnly.MinValue, To: DateOnly.MaxValue),
			budget: budget,
			ct: CancellationToken.None
		);

		await _budgetProgressWriteRepository.DidNotReceive().RecalculateForBudgetAsync(
			budgetId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			fromDate: Arg.Any<DateOnly>(),
			toDate: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenOverlappingBudgetExists_ShouldNotPublishNotification()
	{
		_budgetReadRepository.FindOverlappingAsync(
			userId: Arg.Any<Guid>(),
			categoryId: Arg.Any<Guid>(),
			from: Arg.Any<DateOnly>(),
			to: Arg.Any<DateOnly>(),
			excludeBudgetId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ConflictingBudgetId);

		FinanceTracker.Core.Domains.Budget.Budget budget = BudgetFactory.Create().Value!;

		await _handler.HandleAsync(
			command: new ChangeBudgetPeriodCommand(UserId: budget.UserId, BudgetId: budget.Id, From: DateOnly.MinValue, To: DateOnly.MaxValue),
			budget: budget,
			ct: CancellationToken.None
		);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<BudgetPeriodChangedNotification>());
	}
}
