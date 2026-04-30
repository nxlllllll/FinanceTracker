using FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class UnarchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private UnarchiveAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_handler = new UnarchiveAccountHandler(accountRepository: _accountRepository);
	}

	[Test]
	public async Task HandleAsync_WithArchivedAccount_ShouldSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation(archived: true);

		await _handler.HandleAsync(
			command: new UnarchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyActive_ShouldThrowArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();

		await Assert.That(action: async () => await _handler.HandleAsync(
			command: new UnarchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		)).Throws<UnarchivingException>();
	}
}