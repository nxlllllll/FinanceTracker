using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Application.UseCases.RecurringTransaction.Notifications;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class CreateRecurringTransactionHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IPublisher _publisher = null!;
	private CreateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_publisher = Substitute.For<IPublisher>();

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()());
		
		_handler = new CreateRecurringTransactionHandler(
			recurringTransactionWriteRepository: _writeRepository,
			unitOfWork: _unitOfWork,
			publisher: _publisher,
			dateProvider: FakeDateProvider.Default
		);
	}

	[Test]
	public async Task HandleAsync_WhenValidCommand_ShouldCallCreateAsyncAndReturnId()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: command,
			accounts: account,
			ct: CancellationToken.None
		);

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
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		await _handler.HandleAsync(command: command, accounts: account, ct: CancellationToken.None);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<RecurringTransactionCreatedNotification>(n =>
				n.UserId == command.UserId &&
				n.AccountId == command.AccountId &&
				n.CategoryId == command.CategoryId),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsZero_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: 0m);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(command: command, accounts: account, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsZero_ShouldNotPublishNotification()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: 0m);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		await _handler.HandleAsync(command: command, accounts: account, ct: CancellationToken.None);

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<RecurringTransactionCreatedNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsNegative_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: -100m);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(command: command, accounts: account, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsZero_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 0);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(command: command, accounts: account, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidDayOfMonthException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsOver31_ShouldReturnFailure()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 32);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(command: command, accounts: account, ct: CancellationToken.None);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidDayOfMonthException>();
	}
}