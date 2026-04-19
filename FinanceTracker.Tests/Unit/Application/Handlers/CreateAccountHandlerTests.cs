using FinanceTracker.Application.Accounts.Commands.CreateAccount;
using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Repositories;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers;

public sealed class CreateAccountHandlerTests
{
	private IAccountRepository _accountRepository;
	private IPublisher _publisher;
	private CreateAccountHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new CreateAccountHandler(accountRepository: _accountRepository, publisher: _publisher);
	}
	
	private static CreateAccountCommand CreateCreateAccountCommand(string name = "Карта Сбер")
	{
		return new CreateAccountCommand(
			UserId: Guid.NewGuid(),
			Name: name,
			AccountType: "checking",
			Currency: "RUB",
			InitialBalance: 10000
		);
	}
	
	[Test]
	public async Task Handle_WithValidCommand_ShouldReturnAccountId()
	{
		CreateAccountCommand command = CreateCreateAccountCommand();

		Guid result = await _handler.Handle(command: command, ct: CancellationToken.None);

		await Assert.That(value: result).IsNotEqualTo(notExpected: Guid.Empty);
	}
	
	[Test]
	public async Task Handle_WithValidCommand_ShouldSaveAccount()
	{
		CreateAccountCommand command = CreateCreateAccountCommand();

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			 account: Arg.Is<Account>(account =>
				account.Name == command.Name &&
				account.UserId == command.UserId &&
				account.AccountType == command.AccountType &&
				account.Currency == command.Currency
			), ct: Arg.Any<CancellationToken>()
		);
	}
	
	
	[Test]
	public async Task Handle_WithValidCommand_ShouldPublishNotification()
	{
		CreateAccountCommand command = CreateCreateAccountCommand();

		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<AccountEventsNotification>(predicate: notification => notification.Events.Count == 1),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task Handle_WithEmptyName_ShouldThrowArgumentException()
	{
		CreateAccountCommand command = CreateCreateAccountCommand(name: String.Empty);

		await Assert.That(
			func: async () => await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<ArgumentException>();
	}

	[Test]
	public async Task Handle_WhenExceptionThrown_ShouldNotPublishNotification()
	{
		CreateAccountCommand command = CreateCreateAccountCommand();

		_accountRepository.SaveAsync(account: Arg.Any<Account>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: _ => throw new InvalidOperationException(message: "DB error"));

		await Assert.That(
			func: async () => await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<InvalidOperationException>();

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<AccountEventsNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}