using FinanceTracker.Application.Accounts.Authorization;
using FinanceTracker.Application.Accounts.Commands.ArchiveAccount;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class AccountLoaderTests
{
	private IAccountRepository _accountRepository = null!;
	private AccountLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_loader = new AccountLoader(accountRepository: _accountRepository);
	}

	[Test]
	public async Task LoadAsync_WhenAccountNotFound_ShouldThrowNotFoundException()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Account.Account?>(result: null));

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ArchiveAccountCommand(UserId: Guid.NewGuid(), AccountId: Guid.NewGuid()),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ArchiveAccountCommand(UserId: Guid.NewGuid(), AccountId: account.Id),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		FinanceTracker.Core.Domains.Account.Account result = await _loader.LoadAsync(
			request: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: account.Id);
	}
}