using FinanceTracker.Application.UseCases.Transfers.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

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
			request: CreateTransferCommandFactory.Create(
				fromAccountId: accountId, 
				toAccountId: accountId,
				amount: 100m
			),
			ct: CancellationToken.None
		)).Throws<SameAccountTransferException>();
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
		Account fromAccount = AccountFactory.CreateAccountWithArchivation();
		Account toAccount = AccountFactory.CreateAccountWithArchivation();

		_accountRepository.GetByIdAsync(accountId: fromAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: fromAccount);
		_accountRepository.GetByIdAsync(accountId: toAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: toAccount);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(
				fromAccountId: fromAccount.Id,
				toAccountId: toAccount.Id,
				amount: 100m
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenToAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Account fromAccount = AccountFactory.CreateAccountWithArchivation();
		Account toAccount = AccountFactory.CreateAccountWithArchivation();

		_accountRepository.GetByIdAsync(accountId: fromAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: fromAccount);
		_accountRepository.GetByIdAsync(accountId: toAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: toAccount);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(
				userId: fromAccount.UserId,
				fromAccountId: fromAccount.Id,
				toAccountId: toAccount.Id,
				amount: 100m
			),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenBothAccountsOwnedByUser_ShouldReturnTuple()
	{
		Account fromAccount = AccountFactory.CreateAccountWithArchivation();
		Account toAccount = AccountFactory.CreateAccountWithArchivation(userId: fromAccount.UserId);

		_accountRepository.GetByIdAsync(accountId: fromAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: fromAccount);
		_accountRepository.GetByIdAsync(accountId: toAccount.Id, ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: toAccount);

		Result<(Account FromAccount, Account ToAccount), DomainException> result = await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(
				userId: fromAccount.UserId,
				fromAccountId: fromAccount.Id,
				toAccountId: toAccount.Id,
				amount: 100m
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value.FromAccount.Id).IsEqualTo(expected: fromAccount.Id);
		await Assert.That(value: result.Value.ToAccount.Id).IsEqualTo(expected: toAccount.Id);
	}
}