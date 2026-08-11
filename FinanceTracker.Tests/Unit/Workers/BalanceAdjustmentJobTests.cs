using FinanceTracker.Core.Domains.Abstractions.Rate;
using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.ReadModels.Pending;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.BalanceAdjustment.Job;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class BalanceAdjustmentJobTests
{
	private ITransactionReadRepository _transactionReadRepository = null!;
	private ITransactionRepository _transactionRepository = null!;
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ITransferReadRepository _transferReadRepository = null!;
	private ITransferRepository _transferRepository = null!;
	private ITransferWriteRepository _transferWriteRepository = null!;
	private IAccountRepository _accountRepository = null!;
	private ICurrencyRateReadRepository _currencyRateReadRepository = null!;
	private IUnresolvableEventWriteRepository _unresolvableEventWriteRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IJobExecutionContext _jobContext = null!;
	private BalanceAdjustmentJob _job = null!;

	private static readonly BalanceAdjustmentJobOptions DefaultOptions = new BalanceAdjustmentJobOptions
	{
		MaxRetries = 1,
		BaseDelayMs = 0,
		UseJitter = false,
		BatchSize = 500,
		RateGracePeriodDays = 7
	};

	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;

	private static readonly Currency Usd = Currency.Reconstitute(value: "USD");
	private static readonly Currency Rub = Currency.Reconstitute(value: "RUB");

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_transactionRepository = Substitute.For<ITransactionRepository>();
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_transferReadRepository = Substitute.For<ITransferReadRepository>();
		_transferRepository = Substitute.For<ITransferRepository>();
		_transferWriteRepository = Substitute.For<ITransferWriteRepository>();
		_accountRepository = Substitute.For<IAccountRepository>();
		_currencyRateReadRepository = Substitute.For<ICurrencyRateReadRepository>();
		_unresolvableEventWriteRepository = Substitute.For<IUnresolvableEventWriteRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_jobContext = Substitute.For<IJobExecutionContext>();

		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task>>()?.Invoke());

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: []);

		_job = new BalanceAdjustmentJob(
			transactionReadRepository: _transactionReadRepository,
			transactionRepository: _transactionRepository,
			transactionWriteRepository: _transactionWriteRepository,
			transferReadRepository: _transferReadRepository,
			transferRepository: _transferRepository,
			transferWriteRepository: _transferWriteRepository,
			accountRepository: _accountRepository,
			currencyRateReadRepository: _currencyRateReadRepository,
			unresolvableEventWriteRepository: _unresolvableEventWriteRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			options: new FakeOptionsMonitor<BalanceAdjustmentJobOptions>(value: DefaultOptions),
			logger: Substitute.For<ILogger<BalanceAdjustmentJob>>()
		);
	}

	private Transfer QueueTransfer(
		decimal amountFrom = 100m,
		decimal currentRate = 90m,
		RateStatus rateStatus = RateStatus.Pending,
		TransferStatus status = TransferStatus.Completed,
		decimal? availableRate = 95m,
		DateTimeOffset? rateStatusChangedAt = null)
	{
		Transfer transfer = TransferFactory.Reconstitute(
			amount: amountFrom,
			currencyFrom: "USD",
			currencyTo: "RUB",
			exchangeRate: currentRate,
			rateStatus: rateStatus,
			rateStatusChangedAt: rateStatusChangedAt ?? Now,
			status: status,
			occurredAt: Now
		);

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(
			returnThis: [new PendingRateTransfer(
				TransferId: transfer.Id,
				CurrencyFrom: Usd,
				CurrencyTo: Rub,
				OccurredAt: transfer.OccurredAt,
				RateStatusChangedAt: transfer.RateStatusChangedAt
			)],
			returnThese: []
		);

		_transferRepository.GetByIdAsync(
			transferId: transfer.Id,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: transfer);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Usd,
			targetCurrencyCode: Rub,
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: availableRate);

		return transfer;
	}

	private Account GivenAccount(Guid accountId, bool archived = false)
	{
		Account account = Account.Reconstitute(
			id: accountId,
			userId: Guid.CreateVersion7(),
			name: Name.Create(value: "Карта Сбер").Value!,
			type: AccountType.Checking,
			balance: Money.Reconstitute(amount: 100_000m, currency: Currency.Reconstitute(value: "RUB")),
			isArchived: archived,
			createdAt: Now,
			version: 1
		);

		_accountRepository.GetByIdAsync(
			accountId: accountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: account);

		return account;
	}

	[Test]
	public async Task Execute_WhenTransferRateWasCancelled_ShouldNotTouchAnyBalance()
	{
		Transfer transfer = QueueTransfer(rateStatus: RateStatus.Cancelled, status: TransferStatus.Compensated);

		await _job.Execute(context: _jobContext);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _transferWriteRepository.DidNotReceive().SaveRateResolutionAsync(
			transfer: Arg.Any<Transfer>(),
			ct: Arg.Any<CancellationToken>()
		);

		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 90m)
			.Because(message: "A cancelled rate is settled. Rewriting it would be inventing a correction to a movement that never happened.");
	}

	[Test]
	public async Task Execute_WhenTransferIsCompensatedAfterTheQueueWasRead_ShouldNotTouchAnyBalance()
	{
		Transfer transfer = QueueTransfer(rateStatus: RateStatus.Pending, status: TransferStatus.PendingCredit);

		transfer.Compensate(occurredAt: Now);

		await _job.Execute(context: _jobContext);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _transferWriteRepository.DidNotReceive().SaveRateResolutionAsync(
			transfer: Arg.Any<Transfer>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenRateArrives_ShouldPostTheDeltaAndRecomputeAmountTo()
	{
		Transfer transfer = QueueTransfer(amountFrom: 100m, currentRate: 90m, availableRate: 95m);
		Account toAccount = GivenAccount(accountId: transfer.ToAccountId);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Resolved);
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 95m);
		await Assert.That(value: transfer.AmountTo.Amount).IsEqualTo(expected: Money.ConvertedAmount(amount: 100m, rate: 95m))
			.Because(message: "FT-05: writing the rate without recomputing the amount is what left the history disagreeing with the balance.");

		await Assert.That(value: toAccount.Balance.Amount).IsEqualTo(expected: 100_500m);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: toAccount,
			ct: Arg.Any<CancellationToken>()
		);
		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).SaveRateResolutionAsync(
			transfer: transfer,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldComputeTheDelta_FromTheRateOnTheAggregate()
	{
		Transfer transfer = QueueTransfer(amountFrom: 100m, currentRate: 80m, availableRate: 95m);
		Account toAccount = GivenAccount(accountId: transfer.ToAccountId);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: toAccount.Balance.Amount).IsEqualTo(expected: 101_500m);
	}

	[Test]
	public async Task Execute_WhenRateIsMissingButStillWithinGrace_ShouldLeaveTheRowQueued()
	{
		Transfer transfer = QueueTransfer(availableRate: null, rateStatusChangedAt: Now.AddDays(days: -3));

		await _job.Execute(context: _jobContext);

		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Pending);

		await _transferRepository.DidNotReceive().GetByIdAsync(
			transferId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _transferWriteRepository.DidNotReceive().SaveRateResolutionAsync(
			transfer: Arg.Any<Transfer>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenRateNeverArrived_ShouldWriteItOffAsApproximated_WithoutTouchingTheBalance()
	{
		Transfer transfer = QueueTransfer(currentRate: 90m, availableRate: null, rateStatusChangedAt: Now.AddDays(days: -8));

		await _job.Execute(context: _jobContext);

		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Approximated);
		await Assert.That(value: transfer.RateStatus.IsOpen()).IsFalse()
			.Because(message: "It must leave the queue. A row that can never be resolved and never leaves is the bug.");
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 90m)
			.Because(message: "The placeholder is now the answer. There is nothing to correct it to.");

		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).SaveRateResolutionAsync(
			transfer: transfer,
			ct: Arg.Any<CancellationToken>()
		);
		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _unresolvableEventWriteRepository.DidNotReceive().CreateAsync(
			type: Arg.Any<UnresolvableEventType>(),
			referenceId: Arg.Any<Guid>(),
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTheAccountRefusesTheAdjustment_ShouldEscalateAndLeaveTheQueue()
	{
		Transfer transfer = QueueTransfer(availableRate: 95m);
		GivenAccount(accountId: transfer.ToAccountId, archived: true);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Unresolvable);
		await Assert.That(value: transfer.RateStatus.IsOpen()).IsFalse()
			.Because(message: "Retrying a rejection every night forever is not resilience, it is noise.");
		await Assert.That(value: transfer.ExchangeRate).IsEqualTo(expected: 90m)
			.Because(message: "The correction did not happen, so the rate must not claim it did.");

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			type: UnresolvableEventType.RateAdjustmentFailed,
			referenceId: transfer.Id,
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTheAccountIsGone_ShouldEscalate()
	{
		Transfer transfer = QueueTransfer(availableRate: 95m);

		_accountRepository.GetByIdAsync(
			accountId: transfer.ToAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Account?)null);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Unresolvable);

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			type: UnresolvableEventType.RateAdjustmentFailed,
			referenceId: transfer.Id,
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenResolveFailsAfterAdjust_ShouldNotPersistTheAbandonedAccount()
	{
		Transfer transfer = QueueTransfer(amountFrom: 100m, currentRate: 0.9m, availableRate: 0.00000001m);
		Account toAccount = GivenAccount(accountId: transfer.ToAccountId);

		await _job.Execute(context: _jobContext);

		await Assert.That(value: transfer.RateStatus).IsEqualTo(expected: RateStatus.Unresolvable);
		await Assert.That(value: toAccount.Events.Count).IsGreaterThan(minimum: 0)
			.Because(message: "AdjustBalance did raise an event — which is precisely why it must never be saved.");

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);

		await _unresolvableEventWriteRepository.Received(requiredNumberOfCalls: 1).CreateAsync(
			type: UnresolvableEventType.RateAdjustmentFailed,
			referenceId: transfer.Id,
			reason: Arg.Any<string>(),
			payload: Arg.Any<string>(),
			occurredAt: Arg.Any<DateTimeOffset>(),
			ct: Arg.Any<CancellationToken>()
		);
	}
}
