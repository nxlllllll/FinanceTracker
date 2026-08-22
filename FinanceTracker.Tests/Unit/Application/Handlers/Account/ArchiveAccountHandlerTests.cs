using FinanceTracker.Application.UseCases.Account.Commands.ArchiveAccount;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions;
using FinanceTracker.Core.Exceptions.DomainExceptions.Domain.Account;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using NSubstitute;

namespace FinanceTracker.Tests.Unit.Application.Handlers.Account;

public sealed class ArchiveAccountHandlerTests
{
	private IAccountRepository _accountRepository = null!;
	private ITransferReadRepository _transferReadRepository = null!;
	private ITransactionReadRepository _transactionReadRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private ArchiveAccountHandler _handler = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transferReadRepository = Substitute.For<ITransferReadRepository>();
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();

		_transferReadRepository.HasOpenObligationAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);
		_transactionReadRepository.HasPendingRateAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: false);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: callInfo => callInfo.Arg<Func<Task>>()?.Invoke());

		_handler = new ArchiveAccountHandler(
			accountRepository: _accountRepository,
			transferReadRepository: _transferReadRepository,
			transactionReadRepository: _transactionReadRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default
		);
	}

	private static FinanceTracker.Core.Domains.Account.Account GivenAccountWithBalance(decimal balance)
	{
		return FinanceTracker.Core.Domains.Account.Account.Reconstitute(
			id: Guid.CreateVersion7(),
			userId: Guid.CreateVersion7(),
			name: Name.Create(value: "Карта Сбер").Value!,
			type: AccountType.Checking,
			balance: Money.Reconstitute(amount: balance, currency: FinanceTracker.Core.ValueObjects.Currency.Reconstitute(value: "RUB")),
			isArchived: false,
			createdAt: FakeDateProvider.Default.UtcNow,
			version: 1
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveAccount_ShouldSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation(balance: 0);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WithActiveAccount_ShouldCheckBothObligationSources()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation(balance: 0);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _transferReadRepository.Received(requiredNumberOfCalls: 1).HasOpenObligationAsync(accountId: account.Id, ct: Arg.Any<CancellationToken>());
		await _transactionReadRepository.Received(requiredNumberOfCalls: 1).HasPendingRateAsync(accountId: account.Id, ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldReturnSuccess()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation(archived: true);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue();
	}

	[Test]
	public async Task HandleAsync_WhenAccountAlreadyArchived_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation(archived: true);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBalanceIsNegative_ShouldReturnArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = GivenAccountWithBalance(balance: -500m);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenBalanceIsPositive_ShouldReturnArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation(balance: 500m);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenBalanceIsNegative_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = GivenAccountWithBalance(balance: -500m);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task HandleAsync_WhenBalanceIsNegative_ShouldNotQueryEitherObligationSource()
	{
		FinanceTracker.Core.Domains.Account.Account account = GivenAccountWithBalance(balance: -500m);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _transferReadRepository.DidNotReceive().HasOpenObligationAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>());
		await _transactionReadRepository.DidNotReceive().HasPendingRateAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WhenBalanceIsExactlyZero_ShouldSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = GivenAccountWithBalance(balance: 0m);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsSuccess).IsTrue()
			.Because(message: "Zero is not negative — an account that has settled to nothing owed must remain archivable.");
	}

	[Test]
	public async Task HandleAsync_WhenTransferHasAnOpenObligation_ShouldReturnArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();
		_transferReadRepository.HasOpenObligationAsync(accountId: account.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenTransferHasAnOpenObligation_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();
		_transferReadRepository.HasOpenObligationAsync(accountId: account.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	/// <summary>Short-circuits on the first hit — no reason to also ask about transactions once a
	/// transfer obligation has already blocked the archive.</summary>
	[Test]
	public async Task HandleAsync_WhenTransferHasAnOpenObligation_ShouldNotCheckTransactionRates()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();
		_transferReadRepository.HasOpenObligationAsync(accountId: account.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _transactionReadRepository.DidNotReceive().HasPendingRateAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task HandleAsync_WhenTransactionHasAPendingRate_ShouldReturnArchivingException()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();
		_transactionReadRepository.HasPendingRateAsync(accountId: account.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		Result<Guid, AppException> result = await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await Assert.That(value: result.IsFailure).IsTrue();
		await Assert.That(value: result.Error).IsTypeOf<ArchivingException>();
	}

	[Test]
	public async Task HandleAsync_WhenTransactionHasAPendingRate_ShouldNotSaveAccount()
	{
		FinanceTracker.Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();
		_transactionReadRepository.HasPendingRateAsync(accountId: account.Id, ct: Arg.Any<CancellationToken>()).Returns(returnThis: true);

		await _handler.HandleAsync(
			command: new ArchiveAccountCommand(UserId: account.UserId, AccountId: account.Id),
			account: account,
			ct: CancellationToken.None
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<FinanceTracker.Core.Domains.Account.Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
