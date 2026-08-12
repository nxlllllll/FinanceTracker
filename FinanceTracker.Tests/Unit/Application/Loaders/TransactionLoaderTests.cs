using FinanceTracker.Application.UseCases.Transaction.Authorization;
using FinanceTracker.Application.UseCases.Transaction.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Shared;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class TransactionLoaderTests
{
	private ITransactionRepository _transactionRepository = null!;
	private IAccountRepository _accountRepository = null!;
	private ICategoryRepository _categoryRepository = null!;
	private TransactionLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionRepository = Substitute.For<ITransactionRepository>();
		_accountRepository = Substitute.For<IAccountRepository>();
		_categoryRepository = Substitute.For<ICategoryRepository>();
		_loader = new TransactionLoader(
			accountRepository: _accountRepository,
			transactionRepository: _transactionRepository
		);
	}

	[Test]
	public async Task LoadAsync_WhenTransactionNotFound_ShouldThrowNotFoundException()
	{
		_transactionRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Transaction?>(result: null));

		Result<Transaction, AppException> result = await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(UserId: Guid.CreateVersion7(), TransactionId: Guid.CreateVersion7(), CategoryId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenTransactionBelongsToAnotherUser_ShouldReturnNotFound()
	{
		Transaction transaction = TransactionFactory.Create();
		_transactionRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Transaction?>(result: null));

		Result<Transaction, AppException> result = await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(
				UserId: Guid.CreateVersion7(),
				TransactionId: transaction.Id,
				CategoryId: Guid.CreateVersion7()
			),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenOwner_ShouldReturnTransaction()
	{
		Transaction transaction = TransactionFactory.Create();
		Category category = CategoryFactory.Create().Value!;
		_transactionRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transaction);
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		Result<Transaction, AppException> result = await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(UserId: transaction.UserId, TransactionId: transaction.Id, CategoryId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: transaction.Id);
	}

	[Test]
	public async Task LoadAsync_CreateTransaction_WhenAccountNotFound_ShouldThrowNotFoundException()
	{
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Account?>(result: null));

		Result<Account, AppException> resultAccount = await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(),
			ct: CancellationToken.None
		);

		await Assert.That(value: resultAccount.IsFailure).IsTrue();
		await Assert.That(value: resultAccount.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateTransaction_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Account account = AccountFactory.CreateWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<Account, AppException> resultAccount = await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(userId: Guid.CreateVersion7(), accountId: account.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: resultAccount.IsFailure).IsTrue();
		await Assert.That(value: resultAccount.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateTransaction_WhenOwner_ShouldReturnAccount()
	{
		Account account = AccountFactory.CreateWithArchivation();
		Category category = CategoryFactory.Create().Value!;
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);
		_categoryRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			userId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		Result<Account, AppException> result = await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(userId: account.UserId, accountId: account.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: account.Id);
	}
}
