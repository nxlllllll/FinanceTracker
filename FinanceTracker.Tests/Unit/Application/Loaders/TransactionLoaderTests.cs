using FinanceTracker.Application.Transactions.Authorization;
using FinanceTracker.Application.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Dtos;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class TransactionLoaderTests
{
	private ITransactionReadRepository _transactionReadRepository = null!;
	private IAccountRepository _accountRepository = null!;
	private TransactionLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_accountRepository = Substitute.For<IAccountRepository>();
		_loader = new TransactionLoader(
			accountRepository: _accountRepository,
			transactionReadRepository: _transactionReadRepository
		);
	}

	[Test]
	public async Task LoadAsync_WhenTransactionNotFound_ShouldThrowNotFoundException()
	{
		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<TransactionDto?>(result: null));

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(UserId: Guid.NewGuid(), TransactionId: Guid.NewGuid(), CategoryId: Guid.NewGuid()),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenTransactionBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		TransactionDto transaction = TransactionFactory.Create();
		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transaction);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(UserId: Guid.NewGuid(), TransactionId: transaction.Id, CategoryId: Guid.NewGuid()),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnTransaction()
	{
		TransactionDto transaction = TransactionFactory.Create();
		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transaction);

		TransactionDto result = await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: Guid.NewGuid()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: transaction.Id);
	}
	
	[Test]
	public async Task LoadAsync_CreateTransaction_WhenAccountNotFound_ShouldThrowNotFoundException()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<FinanceTracker.Core.Domains.Account.Account?>(result: null));

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateTransaction_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		await Assert.That(action: async () => await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(userId: Guid.NewGuid(), accountId: account.Id),
			ct: CancellationToken.None
		)).Throws<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateTransaction_WhenOwner_ShouldReturnAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateAccountWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		FinanceTracker.Core.Domains.Account.Account result = await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(userId: account.UserId, accountId: account.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Id).IsEqualTo(expected: account.Id);
	}
}