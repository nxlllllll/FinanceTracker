using FinanceTracker.Application.UseCases.RecurringTransaction.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.RecurringTransaction;

public sealed class CreateRecurringTransactionHandlerTests
{
	private IRecurringTransactionWriteRepository _writeRepository = null!;
	private CreateRecurringTransactionHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_writeRepository = Substitute.For<IRecurringTransactionWriteRepository>();
		_handler = new CreateRecurringTransactionHandler(recurringTransactionWriteRepository: _writeRepository, dateProvider: FakeDateProvider.Default);
	}

	[Test]
	public async Task HandleAsync_WhenValidCommand_ShouldCallCreateAsyncAndReturnId()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create();
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: command,
			account: account,
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
	public async Task HandleAsync_WhenAmountIsZero_ShouldThrowInvalidAmountException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: 0m);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsNegative_ShouldThrowInvalidAmountException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: -100m);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsZero_ShouldThrowInvalidDayOfMonthException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 0);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidDayOfMonthException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsOver31_ShouldThrowInvalidDayOfMonthException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 32);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId).Value!;

		Result<Guid, DomainException> result = await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<InvalidDayOfMonthException>();
	}
}
