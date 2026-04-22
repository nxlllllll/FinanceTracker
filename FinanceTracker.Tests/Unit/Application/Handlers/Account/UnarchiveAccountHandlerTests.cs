using FinanceTracker.Application.Accounts.Commands.UnarchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
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

	private static FinanceTracker.Core.Domains.Account.Account CreateArchivedAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
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
		FinanceTracker.Core.Domains.Account.Account account = CreateArchivedAccount();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		UnarchiveAccountCommand command = new UnarchiveAccountCommand(AccountId: account.Id);
		await _handler.Handle(command: command, ct: CancellationToken.None);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<FinanceTracker.Core.Domains.Account.Account>(predicate: a => !a.IsArchived),
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

		UnarchiveAccountCommand command = new UnarchiveAccountCommand(AccountId: Guid.NewGuid());

		await Assert.That(action: async () =>
			await _handler.Handle(command: command, ct: CancellationToken.None)
		).Throws<NotFoundException>();
	}

	[Test]
	public async Task Handle_WhenAccountNotArchived_ShouldThrowArgumentException()
	{
		FinanceTracker.Core.Domains.Account.Account account = FinanceTracker.Core.Domains.Account.Account.Create(
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