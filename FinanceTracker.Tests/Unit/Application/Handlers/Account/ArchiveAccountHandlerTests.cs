using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class ArchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository;
	private IPublisher _publisher;
	private ArchiveAccountHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new ArchiveAccountHandler(accountRepository: _accountRepository, publisher: _publisher);
	}
	
	private static Core.Domains.Account.Account CreateAccount()
	{
		Core.Domains.Account.Account account = Core.Domains.Account.Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);
		account.ClearEvents();
		return account;
	}
	
	[Test]
    public async Task Handle_WithActiveAccount_ShouldArchiveAccount()
    {
        Core.Domains.Account.Account account = CreateAccount();
        _accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

        ArchiveAccountCommand command = new ArchiveAccountCommand(AccountId: account.Id);
        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<Core.Domains.Account.Account>(predicate: a => a.IsArchived),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithActiveAccount_ShouldPublishNotification()
    {
        Core.Domains.Account.Account account = CreateAccount();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		ArchiveAccountCommand command = new ArchiveAccountCommand(AccountId: account.Id);
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
			ct: Arg.Any<CancellationToken>()
		).Returns(Task.FromResult<Core.Domains.Account.Account?>(result: null));

		ArchiveAccountCommand command = new ArchiveAccountCommand(AccountId: Guid.NewGuid());

		await Assert.That(action: async () => 
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenAccountAlreadyArchived_ShouldThrowArgumentException()
	{
		Core.Domains.Account.Account account = CreateAccount();
		account.Archive();
		account.ClearEvents();

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		ArchiveAccountCommand command = new ArchiveAccountCommand(AccountId: account.Id);

		await Assert.That(action: async () => 
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<ArchivingException>();
	}
}