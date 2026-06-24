using FinanceTracker.Application.UseCases.Transfer.Authorization;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class TransferLoaderTests
{
	private IAccountRepository _accountRepository = null!;
	private IAccountReadRepository _accountReadRepository = null!;
	private TransferLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_accountReadRepository = Substitute.For<IAccountReadRepository>();
		_loader = new TransferLoader(
			accountRepository: _accountRepository,
			accountReadRepository: _accountReadRepository
		);
	}

	[Test]
	public async Task LoadAsync_WhenSameAccount_ShouldThrowInvalidOperationException()
	{
		Guid accountId = Guid.CreateVersion7();

		Result<TransferAccounts, DomainException> result = await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(
				fromAccountId: accountId, 
				toAccountId: accountId,
				amount: 100m
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<SameAccountTransferException>();
	}

	[Test]
	public async Task LoadAsync_WhenFromAccountNotFound_ShouldThrowNotFoundException()
	{
		_accountRepository.GetByIdAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>())
			.Returns(returnThis: Task.FromResult<Account?>(result: null));

		Result<TransferAccounts, DomainException> result = await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenFromAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Account fromAccount = AccountFactory.CreateWithArchivation();
		Account toAccount = AccountFactory.CreateWithArchivation();

		_accountRepository.GetByIdAsync(
			accountId: fromAccount.Id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: fromAccount);

		Result<TransferAccounts, DomainException> resultFrom = await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(
				fromAccountId: fromAccount.Id,
				toAccountId: toAccount.Id,
				amount: 100m
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: resultFrom.IsFailure).IsTrue();
		await Assert.That(value: resultFrom.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenToAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Account fromAccount = AccountFactory.CreateWithArchivation();
		Account toAccount = AccountFactory.CreateWithArchivation();

		_accountRepository.GetByIdAsync(
			accountId: fromAccount.Id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: fromAccount);

		_accountReadRepository.GetByIdAsync(
			accountId: toAccount.Id,
			userId: fromAccount.UserId, 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<AccountReadModel?>(result: null));

		Result<TransferAccounts, DomainException> resultTo = await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(
				userId: fromAccount.UserId,
				fromAccountId: fromAccount.Id,
				toAccountId: toAccount.Id,
				amount: 100m
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: resultTo.IsFailure).IsTrue();
		await Assert.That(value: resultTo.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenBothAccountsOwnedByUser_ShouldReturnTransferAccounts()
	{
		Account fromAccount = AccountFactory.CreateWithArchivation();
		AccountReadModel toAccountReadModel = AccountFactory.CreateReadModel(userId: fromAccount.UserId, currency: "USD");

		_accountRepository.GetByIdAsync(
			accountId: fromAccount.Id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: fromAccount);
		_accountReadRepository.GetByIdAsync(
			accountId: toAccountReadModel.Id,
			userId: fromAccount.UserId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: toAccountReadModel);

		Result<TransferAccounts, DomainException> result = await _loader.LoadAsync(
			request: CreateTransferCommandFactory.Create(
				userId: fromAccount.UserId,
				fromAccountId: fromAccount.Id,
				toAccountId: toAccountReadModel.Id,
				amount: 100m
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.FromAccount.Id).IsEqualTo(expected: fromAccount.Id);
		await Assert.That(value: result.Value!.ToAccountCurrency).IsEqualTo(expected: toAccountReadModel.Balance.Currency);
	}
}