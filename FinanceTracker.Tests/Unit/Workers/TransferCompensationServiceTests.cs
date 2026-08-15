using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.TransferProjection.Services;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using AccountAggregate = FinanceTracker.Core.Domains.Account.Account;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class TransferCompensationServiceTests
{
	private IAccountRepository _accountRepository = null!;
	private ITransferRepository _transferRepository = null!;
	private ITransferWriteRepository _transferWriteRepository = null!;
	private IUnresolvableEventWriteRepository _unresolvableEventWriteRepository = null!;
	private TransferCompensationService _service = null!;

	private static readonly Guid TransferId = Guid.CreateVersion7();
	private static readonly Guid FromAccountId = Guid.CreateVersion7();
	private static readonly PendingCreditTransfer Pending = new PendingCreditTransfer(
		TransferId: TransferId,
		FromAccountId: FromAccountId,
		Amount: 500m,
		OccurredAt: FakeDateProvider.Default.UtcNow
	);

	[Before(hookType: Test)]
	public void Setup()
	{
		_accountRepository = Substitute.For<IAccountRepository>();
		_transferRepository = Substitute.For<ITransferRepository>();
		_transferWriteRepository = Substitute.For<ITransferWriteRepository>();
		_unresolvableEventWriteRepository = Substitute.For<IUnresolvableEventWriteRepository>();

		_service = new TransferCompensationService(
			accountRepository: _accountRepository,
			transferRepository: _transferRepository,
			transferWriteRepository: _transferWriteRepository,
			unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
			dateProvider: FakeDateProvider.Default,
			logger: NullLogger<TransferCompensationService>.Instance
		);
	}

	private static Transfer PendingCreditTransferAggregate() => Transfer.Create(
		createdAt: FakeDateProvider.Default.UtcNow,
		userId: Guid.CreateVersion7(),
		fromAccountId: FromAccountId,
		toAccountId: Guid.CreateVersion7(),
		amount: 500m,
		currencyFrom: Currency.Create(value: "RUB").Value,
		currencyTo: Currency.Create(value: "RUB").Value,
		exchangeRate: 1m,
		rateStatus: RateStatus.Exact,
		description: null,
		occurredAt: FakeDateProvider.Default.UtcNow
	).Value!;

	private static AccountAggregate FundedAccount(decimal balance = 1000m) => AccountAggregate.Create(
		occurredAt: FakeDateProvider.Default.UtcNow,
		userId: Guid.CreateVersion7(),
		name: Name.Create(value: "Main").Value,
		type: AccountType.Cash,
		currency: Currency.Create(value: "RUB").Value,
		balance: balance
	).Value!;

	private static AccountAggregate ArchivedAccount()
	{
		AccountAggregate account = FundedAccount(balance: 0m);
		account.Archive(occurredAt: FakeDateProvider.Default.UtcNow);
		return account;
	}

	private Task CompensateAsync()
		=> _service.CompensateAsync(pendingTransfer: Pending, ct: CancellationToken.None);

	[Test]
	public async Task AMissingTransferIsLeftAlone()
	{
		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Transfer?)null);

		await CompensateAsync();

		await Assert.That(value: _accountRepository.ReceivedCalls()).IsEmpty();
		await Assert.That(value: _transferWriteRepository.ReceivedCalls()).IsEmpty();
		await Assert.That(value: _unresolvableEventWriteRepository.ReceivedCalls()).IsEmpty();
	}

	[Test]
	public async Task ATransferThatIsNoLongerPendingIsNotRefundedAgain()
	{
		Transfer transfer = PendingCreditTransferAggregate();
		transfer.Compensate(occurredAt: FakeDateProvider.Default.UtcNow);

		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transfer);

		await CompensateAsync();

		await Assert.That(value: _accountRepository.ReceivedCalls()).IsEmpty()
			.Because(message: "a transfer already settled must not be compensated a second time");

		await Assert.That(value: _transferWriteRepository.ReceivedCalls()).IsEmpty();
	}

	[Test]
	public async Task AMissingSourceAccountIsEscalatedRatherThanIgnored()
	{
		Transfer transfer = PendingCreditTransferAggregate();

		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transfer);
		_accountRepository.GetByIdAsync(
			accountId: FromAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (AccountAggregate?)null);

		await CompensateAsync();

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			type: UnresolvableEventType.TransferCompensation,
			referenceId: TransferId,
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);

		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Failed);

		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).SaveStatusAsync(
			transfer: transfer,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ARefusedRefundIsEscalatedAndLeavesTheAccountUntouched()
	{
		Transfer transfer = PendingCreditTransferAggregate();

		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transfer);
		_accountRepository.GetByIdAsync(
			accountId: FromAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: ArchivedAccount());

		await CompensateAsync();

		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Failed);

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			type: UnresolvableEventType.TransferCompensation,
			referenceId: TransferId,
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<AccountAggregate>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ASuccessfulCompensationRefundsAndSettlesTogether()
	{
		Transfer transfer = PendingCreditTransferAggregate();
		AccountAggregate account = FundedAccount();
		decimal balanceBefore = account.Balance.Amount;

		_transferRepository.GetByIdAsync(
			transferId: TransferId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transfer);
		_accountRepository.GetByIdAsync(
			accountId: FromAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		await CompensateAsync();

		await Assert.That(value: account.Balance).IsEqualTo(expected: Money.Positive(amount: balanceBefore + Pending.Amount, currency: account.Balance.Currency).Value);
		await Assert.That(value: transfer.Status).IsEqualTo(expected: TransferStatus.Compensated);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(account: account, ct: Arg.Any<CancellationToken>());
		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).SaveStatusAsync(transfer: transfer, ct: Arg.Any<CancellationToken>());

		await Assert.That(value: _unresolvableEventWriteRepository.ReceivedCalls()).IsEmpty()
			.Because(message: "a compensation that worked must not also file an incident");
	}
}
