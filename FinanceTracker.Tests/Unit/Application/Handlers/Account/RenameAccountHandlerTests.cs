using FinanceTracker.Application.Accounts.Commands.RenameAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
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
		_handler = new RenameAccountHandler(accountRepository: _accountRepository);
	}

	private static FinanceTracker.Core.Domains.Account.Account CreateAccount(string name = "Карта Сбер")
	{
		FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
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
		FinanceTracker.Core.Domains.Account.Account account = CreateAccount();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		RenameAccountCommand command = new RenameAccountCommand(AccountId: account.Id, NewName: "Карта Тинькофф");
		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => a.Name == "Карта Тинькофф"),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Handle_WhenAccountNotFound_ShouldThrowInvalidOperationException()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Account.Account?>(result: null));

		RenameAccountCommand command = new RenameAccountCommand(AccountId: Guid.NewGuid(), NewName: "Карта Тинькофф");

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}
}