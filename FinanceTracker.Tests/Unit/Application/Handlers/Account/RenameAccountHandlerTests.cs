using FinanceTracker.Application.UseCases.Account.Commands.RenameAccount;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.ValueObjects;
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
			command: new RenameAccountCommand(UserId: account.UserId, AccountId: account.Id, NewName: Name.Create(value: "����� ��������").Value),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Name.Value == "����� ��������"),
			ct: Arg.Any<CancellationToken>()
		);
	}
	
	[Test]
	public async Task HandleAsync_WhenNameUnchanged_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.Create(name: "����� ����").Value!;
		Name sameName = Name.Create(value: "����� ����").Value!;

		account.ClearEvents();
		
		await _handler.HandleAsync(
			command: new RenameAccountCommand(UserId: account.UserId, AccountId: account.Id, NewName: sameName),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
