using FinanceTracker.Application.UseCases.Transactions.Authorization;
using FinanceTracker.Application.UseCases.Transactions.Commands.ChangeTransactionCategory;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Category;
using FinanceTracker.Core.Domains.Transaction;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Category;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Results;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Loaders;

public sealed class TransactionLoaderTests
{
	private ITransactionReadRepository _transactionReadRepository = null!;
	private IAccountRepository _accountRepository = null!;
	private ICategoryReadRepository _categoryReadRepository = null!;
	private TransactionLoader _loader = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_accountRepository = Substitute.For<IAccountRepository>();
		_categoryReadRepository = Substitute.For<ICategoryReadRepository>();
		_loader = new TransactionLoader(
			accountRepository: _accountRepository,
			categoryRepository: _categoryReadRepository,
			transactionReadRepository: _transactionReadRepository
		);
	}

	[Test]
	public async Task LoadAsync_WhenTransactionNotFound_ShouldThrowNotFoundException()
	{
		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: Task.FromResult<Transaction?>(result: null));

		Result<Transaction, DomainException> result = await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(UserId: Guid.CreateVersion7(), TransactionId: Guid.CreateVersion7(), CategoryId: Guid.CreateVersion7()),
			ct: CancellationToken.None
		);
		
		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_WhenTransactionBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Transaction transaction = TransactionFactory.Create();
		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transaction);

		Result<Transaction, DomainException> result = await _loader.LoadAsync(
			request: new ChangeTransactionCategoryCommand(UserId: Guid.CreateVersion7(), TransactionId: transaction.Id, CategoryId: Guid.CreateVersion7()),
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
		_transactionReadRepository.GetByIdAsync(
			transactionId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transaction);
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		Result<Transaction, DomainException> result = await _loader.LoadAsync(
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

		Result<Account, DomainException> resultAccount = await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(),
			ct: CancellationToken.None
		);
		
		await Assert.That(value: resultAccount.IsFailure).IsTrue();
		await Assert.That(value: resultAccount.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateTransaction_WhenAccountBelongsToAnotherUser_ShouldThrowNotFoundException()
	{
		Account account = AccountFactory.CreateAccountWithArchivation();
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		Result<Account, DomainException> resultAccount = await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(userId: Guid.CreateVersion7(), accountId: account.Id),
			ct: CancellationToken.None
		);
		
		await Assert.That(value: resultAccount.IsFailure).IsTrue();
		await Assert.That(value: resultAccount.Error).IsTypeOf<NotFoundException>();
	}

	[Test]
	public async Task LoadAsync_CreateTransaction_WhenOwner_ShouldReturnAccount()
	{
		Account account = AccountFactory.CreateAccountWithArchivation();
		Category category = CategoryFactory.Create().Value!;
		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(), 
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);
		_categoryReadRepository.GetByIdAsync(
			categoryId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: category);

		Result<Account, DomainException> result = await _loader.LoadAsync(
			request: CreateTransactionCommandFactory.Create(userId: account.UserId, accountId: account.Id),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
		await Assert.That(value: result.Value!.Id).IsEqualTo(expected: account.Id);
	}
}