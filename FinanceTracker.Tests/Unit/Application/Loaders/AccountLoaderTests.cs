using FinanceTracker.Application.UseCases.Account.Authorization;
using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
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
		).Returns(returnThis: Task.FromResult<Account?>(result: null));

		Result<Account, AppException> result = await _loader.LoadAsync(
			request: new ArchiveAccountCommand(UserId: Guid.CreateVersion7(), AccountId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Account account = AccountFactory.CreateWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<Account, AppException> result = await _loader.LoadAsync(
			request: new ArchiveAccountCommand(UserId: Guid.CreateVersion7(), AccountId: account.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnAccount()
	{
		Account account = AccountFactory.CreateWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<Account, AppException> result = await _loader.LoadAsync(
			request: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: account.Id);
	}
}
