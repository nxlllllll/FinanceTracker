using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
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

	[Test]
	public async Task HandleAsync_WithActiveAccount_ShouldSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		)).Throws<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		)).Throws<ArchivingException>();

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}