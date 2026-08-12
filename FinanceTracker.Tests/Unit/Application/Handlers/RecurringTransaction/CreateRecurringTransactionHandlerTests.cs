using FinanceTracker.Application.Behaviours.Notification;
using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.RecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Exceptions.DomainExceptions.Validation;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Category;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class CreateRecurringTransactionHandlerTests
{
	private ICategoryReadRepository _categoryReadRepository = null!;
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPostCommitNotifications _postCommitNotifications = null!;
	private CreateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_postCommitNotifications = Substitute.For<IPostCommitNotifications>();

		SetupCategory(type: CategoryType.Expense);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_handler = new CreateRecurringTransactionHandler(
			categoryReadRepository: _categoryReadRepository,
			recurringTransactionWriteRepository: _writeRepository,
			unitOfWork: _unitOfWork,
			postCommitNotifications: _postCommitNotifications,
			dateProvider: FakeDateProvider.Default
		);
	}

	private void SetupCategory(CategoryType type = CategoryType.Expense, bool archived = false)
	{
		CategoryReadModel category = CategoryFactory.CreateReadModel(type: type, archived: archived);

		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);
	}

	[Test]
	public async Task HandleAsync_WhenValidCommand_ShouldCallCreateAsyncAndReturnId()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await _writeRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			recurringTransaction: Arg.Any<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction>(),
			ct: Arg.Any<CancellationToken>()
		);
		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value).IsNotEqualTo(notExpected: Guid.Empty);
	}

	[Test]
	public async Task HandleAsync_WhenValidCommand_ShouldPublishNotification()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();

		await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		_postCommitNotifications.Received(requiredNumberOfCalls: 1).Stage(notification: Arg.Is<RecurringTransactionCreatedNotification>(n =>
			n!.UserId == command.UserId &&
			n.AccountId == command.AccountId &&
			n.CategoryId == command.CategoryId
		));
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsZero_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: 0m);

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsZero_ShouldNotPublishNotification()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: 0m);

		await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		_postCommitNotifications.DidNotReceive().Stage(notification: Arg.Any<RecurringTransactionCreatedNotification>());
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsNegative_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: -100m);

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsZero_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 0);

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidDayOfMonthException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsOver31_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 32);

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidDayOfMonthException>();
	}

	[Test]
	public async Task HandleAsync_WhenCategoryNotFound_ShouldReturnNotFoundException()
	{
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (CategoryReadModel?)null);

		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task HandleAsync_WhenCategoryNotFound_ShouldNotCreateRecurringTransaction()
	{
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (CategoryReadModel?)null);

		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();

		await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await _writeRepository.DidNotReceive().CreateAsync(
			recurringTransaction: Arg.Any<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenDebitDirectionWithIncomeCategory_ShouldReturnInvalidTransactionDirectionException()
	{
		SetupCategory(type: CategoryType.Income);

		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(direction: FinanceTracker.Core.Domains.Account.DirectionType.Debit);

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransactionDirectionException>();
	}

	[Test]
	public async Task HandleAsync_WhenCreditDirectionWithExpenseCategory_ShouldReturnInvalidTransactionDirectionException()
	{
		SetupCategory(type: CategoryType.Expense);

		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(direction: FinanceTracker.Core.Domains.Account.DirectionType.Credit);

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidTransactionDirectionException>();
	}

	[Test]
	public async Task HandleAsync_WhenDirectionMismatchesCategory_ShouldNotCreateRecurringTransaction()
	{
		SetupCategory(type: CategoryType.Income);

		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(direction: FinanceTracker.Core.Domains.Account.DirectionType.Debit);

		await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await _writeRepository.DidNotReceive().CreateAsync(
			recurringTransaction: Arg.Any<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenCreditDirectionWithIncomeCategory_ShouldSucceed()
	{
		SetupCategory(type: CategoryType.Income);

		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(direction: FinanceTracker.Core.Domains.Account.DirectionType.Credit);

		Result<Guid, AppException> result = await _handler.HandleAsync(command: command, ct: CancellationToken.None);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}
}
