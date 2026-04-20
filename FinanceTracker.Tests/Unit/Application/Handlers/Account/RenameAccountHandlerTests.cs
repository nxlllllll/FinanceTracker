using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class RenameAccountHandlerTests
{
	private IAccountRepository _accountRepository;
	private IPublisher _publisher;
	private RenameAccountHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new RenameAccountHandler(accountRepository: _accountRepository, publisher: _publisher);
	}
	
	private static Core.Domains.Account.Account CreateAccount(string name = "Карта Сбер")
	{
		Core.Domains.Account.Account account = Core.Domains.Account.Account.Create(
			userId: Guid.NewGuid(),
			name: name,
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);
		account.ClearEvents();
		return account;
	}
	
	[Test]
	public async Task Handle_WithValidCommand_ShouldRenameAccount()
	{
		Core.Domains.Account.Account account = CreateAccount();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		RenameAccountCommand command = new RenameAccountCommand(AccountId: account.Id, NewName: "Карта Тинькофф");
		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<Core.Domains.Account.Account>(predicate: a => a.Name == "Карта Тинькофф"),
			ct: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task Handle_WithValidCommand_ShouldPublishNotification()
	{
		Core.Domains.Account.Account account = CreateAccount();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		RenameAccountCommand command = new RenameAccountCommand(AccountId: account.Id, NewName: "Карта Тинькофф");
		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _publisher.Received(requiredNumberOfCalls: 1).Publish(
			notification: Arg.Is<AccountEventsNotification>(predicate: notification => notification.Events.Count == 1),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task Handle_WhenAccountNotFound_ShouldThrowInvalidOperationException()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct:	Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Core.Domains.Account.Account?>(result: null));

		RenameAccountCommand command = new RenameAccountCommand(AccountId: Guid.NewGuid(), NewName: "Карта Тинькофф");

		await Assert.That(action: async () => 
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenAccountNotFound_ShouldNotPublishNotification()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct:	Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Core.Domains.Account.Account?>(result: null));

		RenameAccountCommand command = new RenameAccountCommand(AccountId: Guid.NewGuid(), NewName: "Карта Тинькофф");

		await Assert.That(action: async () => 
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();

		await _publisher.DidNotReceive().Publish(
			notification: Arg.Any<AccountEventsNotification>(),
			cancellationToken: Arg.Any<CancellationToken>()
		);
	}
}