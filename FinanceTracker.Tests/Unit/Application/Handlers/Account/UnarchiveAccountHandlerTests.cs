using FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories;
using MediatR;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class UnarchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository;
	private IPublisher _publisher;
	private UnarchiveAccountHandler _handler;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_publisher = Substitute.For<IPublisher>();
		_handler = new UnarchiveAccountHandler(accountRepository: _accountRepository, publisher: _publisher);
	}

	private static Core.Domains.Account.Account CreateArchivedAccount()
	{
		Core.Domains.Account.Account account = Core.Domains.Account.Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);
		account.Archive();
		account.ClearEvents();
		return account;
	}
	
	[Test]
    public async Task Handle_WithArchivedAccount_ShouldUnarchiveAccount()
    {
		Core.Domains.Account.Account account = CreateArchivedAccount();
        _accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

        UnarchiveAccountCommand command = new UnarchiveAccountCommand(AccountId: account.Id);
        await _handler.Handle(command: command, ct: CancellationToken.None);

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: Arg.Is<Core.Domains.Account.Account>(predicate: a => !a.IsArchived),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Handle_WithArchivedAccount_ShouldPublishNotification()
    {
		Core.Domains.Account.Account account = CreateArchivedAccount();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

        UnarchiveAccountCommand command = new UnarchiveAccountCommand(AccountId: account.Id);
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

		UnarchiveAccountCommand command = new UnarchiveAccountCommand(AccountId: Guid.NewGuid());

		await Assert.That(action: async () => 
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
    }

    [Test]
    public async Task Handle_WhenAccountNotArchived_ShouldThrowArgumentException()
    {
		Core.Domains.Account.Account account = Core.Domains.Account.Account.Create(
			userId: Guid.NewGuid(),
			name: "Карта Сбер",
			accountType: "checking",
			currency: "RUB",
			balance: 0
		);
		account.ClearEvents();

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		UnarchiveAccountCommand command = new UnarchiveAccountCommand(AccountId: account.Id);

		await Assert.That(action: async () => 
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<UnarchivingException>();
    }
}