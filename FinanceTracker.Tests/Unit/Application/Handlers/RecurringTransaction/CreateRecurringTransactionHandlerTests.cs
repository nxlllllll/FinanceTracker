using FinanceTracker.Application.RecurringTransactions.Commands.CreateRecurringTransaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.RecurringTransaction;
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
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId);

		Guid result = await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		);

		await _writeRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			recurringTransaction: Arg.Any<FinanceTracker.Core.Domains.RecurringTransaction.RecurringTransaction>(),
			ct: Arg.Any<CancellationToken>()
		);

		await Assert.That(result).IsNotEqualTo(Guid.Empty);
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsZero_ShouldThrowInvalidAmountException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: 0m);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId);

		await Assert.That(async () => await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		)).Throws<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenAmountIsNegative_ShouldThrowInvalidAmountException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(amount: -100m);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId);

		await Assert.That(async () => await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		)).Throws<InvalidAmountException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsZero_ShouldThrowInvalidDayOfMonthException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 0);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId);

		await Assert.That(async () => await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		)).Throws<InvalidDayOfMonthException>();
	}

	[Test]
	public async Task HandleAsync_WhenDayOfMonthIsOver31_ShouldThrowInvalidDayOfMonthException()
	{
		CreateRecurringTransactionCommand command = CreateRecurringTransactionCommandFactory.Create(dayOfMonth: 32);
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(userId: command.UserId);

		await Assert.That(async () => await _handler.HandleAsync(
			command: command,
			account: account,
			ct: CancellationToken.None
		)).Throws<InvalidDayOfMonthException>();
	}
}