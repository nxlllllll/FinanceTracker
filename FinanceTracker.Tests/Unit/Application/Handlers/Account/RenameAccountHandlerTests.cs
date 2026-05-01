using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class RenameAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private RenameAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_handler = new RenameAccountHandler(accountRepository: _accountRepository, dateProvider: FakeDateProvider.Default);
	}

	[Test]
	public async Task HandleAsync_ShouldSaveAccountWithNewName()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();

		await _handler.HandleAsync(
			command: new RenameAccountCommand(UserId: account.UserId, AccountId: account.Id, NewName: "Новое название"),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Name == "Новое название"),
			ct: Arg.Any<CancellationToken>()
		);
	}
}