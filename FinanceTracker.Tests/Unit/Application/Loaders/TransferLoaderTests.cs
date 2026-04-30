using FinanceTracker.Application.Transfers.Authorization;
using FinanceTracker.Application.Transfers.Commands;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Transfer;

public sealed class TransferLoaderTests
{
	private IAccountRepository _accountRepository = null!;
	private TransferLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_loader = new TransferLoader(accountRepository: _accountRepository);
	}

	[Test]
	public async Task LoadAsync_WhenSameAccount_ShouldThrowInvalidOperationException()
	{
		Guid accountId = Guid.NewGuid();

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new CreateTransferCommand(UserId: Guid.NewGuid(), FromAccountId: accountId, ToAccountId: accountId, Amount: 100m, Description: null, OccurredAt: DateTime.UtcNow),
			ct: CancellationToken.None
		)).Throws<InvalidOperationException>();
	}

	[Test]
	public async Task LoadAsync_WhenFromAccountNotFound_ShouldThrowNotFoundException()
	{
		_accountRepository.GetByIdAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Account.Account?>(result: null));

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenFromAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateAccountWithArchivation();
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateAccountWithArchivation();

		_accountRepository.GetByIdAsync(accountId: fromAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: fromAccount);
		_accountRepository.GetByIdAsync(accountId: toAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: toAccount);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new CreateTransferCommand(
				UserId: Guid.NewGuid(),
				FromAccountId: fromAccount.Id,
				ToAccountId: toAccount.Id,
				Amount: 100m, Description: null, OccurredAt: DateTime.UtcNow
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenToAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateAccountWithArchivation();
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateAccountWithArchivation();

		_accountRepository.GetByIdAsync(accountId: fromAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: fromAccount);
		_accountRepository.GetByIdAsync(accountId: toAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: toAccount);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new CreateTransferCommand(
				UserId: fromAccount.UserId,
				FromAccountId: fromAccount.Id,
				ToAccountId: toAccount.Id,
				Amount: 100m, Description: null, OccurredAt: DateTime.UtcNow
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenBothAccountsOwnedByUser_ShouldReturnTuple()
	{
		FinanceTracker.Core.Domains.Account.Account fromAccount = AccountFactory.CreateAccountWithArchivation();
		FinanceTracker.Core.Domains.Account.Account toAccount = AccountFactory.CreateAccountWithArchivation(userId: fromAccount.UserId);

		_accountRepository.GetByIdAsync(accountId: fromAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: fromAccount);
		_accountRepository.GetByIdAsync(accountId: toAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: toAccount);

		(FinanceTracker.Core.Domains.Account.Account from, FinanceTracker.Core.Domains.Account.Account to) = await _loader.LoadAsync(
			request: new CreateTransferCommand(
				UserId: fromAccount.UserId,
				FromAccountId: fromAccount.Id,
				ToAccountId: toAccount.Id,
				Amount: 100m, Description: null, OccurredAt: DateTime.UtcNow
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: from.Id).IsEqualTo(expected: fromAccount.Id);
		await Assert.That(value: to.Id).IsEqualTo(expected: toAccount.Id);
	}
}