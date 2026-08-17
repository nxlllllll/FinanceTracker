using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.Budget.Commands.CreateBudget;
using FinanceTracker.Application.UseCases.Budget.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Budget;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Budget;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Budget;

public sealed class CreateBudgetHandlerTests
{
	private static readonly Guid ConflictingBudgetId = Guid.CreateVersion7();

	private IBudgetReadRepository _budgetReadRepository = null!;
	private IBudgetWriteRepository _budgetWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private CreateBudgetHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_budgetReadRepository = Substitute.For<IBudgetReadRepository>();
		_budgetWriteRepository = Substitute.For<IBudgetWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task<Guid?>>>(),
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

		_handler = new CreateBudgetHandler(
			budgetReadRepository: _budgetReadRepository,
			budgetWriteRepository: _budgetWriteRepository,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static CreateBudgetCommand ValidCommand() => new CreateBudgetCommand(
		UserId: Guid.CreateVersion7(),
		CategoryId: Guid.CreateVersion7(),
		Currency: FinanceTracker.Core.ValueObjects.Currency.Create(value: "RUB").Value,
		Amount: 10000m,
		From: new DateOnly(year: 2025, month: 1, day: 1),
		To: new DateOnly(year: 2025, month: 1, day: 31)
	);

	private void ReturnConflict() => _budgetReadRepository.FindOverlappingAsync(
		userId: Arg.Any<Guid>(),
		categoryId: Arg.Any<Guid>(),
		from: Arg.Any<DateOnly>(),
		to: Arg.Any<DateOnly>(),
		excludeBudgetId: Arg.Any<Guid?>(),
		ct: Arg.Any<CancellationToken>()
	).Returns(returnThis: ConflictingBudgetId);

	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnBudgetId()
	{
		CreateBudgetCommand command = ValidCommand();

		Result<Guid, AppException> result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotEqualTo(notExpected: Guid.Empty);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldCallCreateAsync()
	{
		CreateBudgetCommand command = ValidCommand();

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _budgetWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			budget: Arg.Is<FinanceTracker.Core.Domains.Budget.Budget>(b =>
				b!.UserId == command.UserId &&
				b.CategoryId == command.CategoryId &&
				b.Amount.Currency == command.Currency &&
				b.Amount.Amount == command.Amount &&
				b.From == command.From &&
				b.To == command.To),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WithValidCommand_ShouldPublishNotification()
	{
		CreateBudgetCommand command = ValidCommand();

		await _handler.Handle(command: command, ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<BudgetCreatedNotification>(n =>
			n!.UserId == command.UserId &&
			n.CategoryId == command.CategoryId
		));
	}

	[Test]
	public async Task Handle_WhenOverlappingBudgetExists_ShouldReturnFailure()
	{
		ReturnConflict();

		Result<Guid, AppException> result = await _handler.Handle(command: ValidCommand(), ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<OverlappingBudgetException>();
	}

	[Test]
	public async Task Handle_WhenOverlappingBudgetExists_ShouldNameTheConflictingBudget()
	{
		ReturnConflict();

		Result<Guid, AppException> result = await _handler.Handle(command: ValidCommand(), ct: CancellationToken.None);

		OverlappingBudgetException error = (OverlappingBudgetException)result.Error!;

		await Assert.That(value: error.ConflictingBudgetId).IsEqualTo(expected: ConflictingBudgetId);
	}

	[Test]
	public async Task Handle_WhenOverlappingBudgetExists_ShouldNotCallCreateAsync()
	{
		ReturnConflict();

		await _handler.Handle(command: ValidCommand(), ct: CancellationToken.None);

		await _budgetWriteRepository.DidNotReceive().CreateAsync(
			budget: Arg.Any<FinanceTracker.Core.Domains.Budget.Budget>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenOverlappingBudgetExists_ShouldNotPublishNotification()
	{
		ReturnConflict();

		await _handler.Handle(command: ValidCommand(), ct: CancellationToken.None);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<BudgetCreatedNotification>());
	}
}
