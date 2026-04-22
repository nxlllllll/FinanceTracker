using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class ArchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private ArchiveAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_handler = new ArchiveAccountHandler(accountRepository: _accountRepository);
	}

	private static FinanceTracker.Core.Domains.Account.Account CreateAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
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
		FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		ArchiveAccountCommand command = new ArchiveAccountCommand(AccountId: account.Id);
		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.IsArchived),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenAccountNotFound_ShouldThrowInvalidOperationException()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(Task.FromResult<FinanceTracker.Core.Domains.Account.Account?>(result: null));

		ArchiveAccountCommand command = new ArchiveAccountCommand(AccountId: Guid.NewGuid());

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenAccountAlreadyArchived_ShouldThrowArgumentException()
	{
		FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
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