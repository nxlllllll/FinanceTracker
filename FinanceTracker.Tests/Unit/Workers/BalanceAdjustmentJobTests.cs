using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Persistence;
using FinanceTracker.Core.ReadModels;
using FinanceTracker.Core.Repositories.Account;
using FinanceTracker.Core.Repositories.Currency;
using FinanceTracker.Core.Repositories.Transaction;
using FinanceTracker.Core.Repositories.Transfer;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.BalanceAdjustment.Job;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class BalanceAdjustmentJobTests
{
	private ITransactionReadRepository _transactionReadRepository = null!;
	private ITransactionWriteRepository _transactionWriteRepository = null!;
	private ITransferReadRepository _transferReadRepository = null!;
	private ITransferWriteRepository _transferWriteRepository = null!;
	private IAccountRepository _accountRepository = null!;
	private ICurrencyRateReadRepository _currencyRateReadRepository = null!;
	private IUnitOfWork _unitOfWork = null!;
	private IJobExecutionContext _jobContext = null!;
	private BalanceAdjustmentJob _job = null!;

	private static readonly BalanceAdjustmentJobOptions DefaultOptions = new BalanceAdjustmentJobOptions
	{
		MaxRetries = 1,
		BaseDelayMs = 0,
		UseJitter = false,
		BatchSize = 500
	};

	[Before(hookType: Test)]
	public void Setup()
	{
		_transactionReadRepository = Substitute.For<ITransactionReadRepository>();
		_transactionWriteRepository = Substitute.For<ITransactionWriteRepository>();
		_transferReadRepository = Substitute.For<ITransferReadRepository>();
		_transferWriteRepository = Substitute.For<ITransferWriteRepository>();
		_accountRepository = Substitute.For<IAccountRepository>();
		_currencyRateReadRepository = Substitute.For<ICurrencyRateReadRepository>();
		_unitOfWork = Substitute.For<IUnitOfWork>();
		_jobContext = Substitute.For<IJobExecutionContext>();

		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_unitOfWork.ExecuteInTransactionAsync(
			operation: Arg.Any<Func<Task>>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: call => call.Arg<Func<Task>>()?.Invoke());

		SetupEmptyRepositories();

		_job = new BalanceAdjustmentJob(
			transactionReadRepository: _transactionReadRepository,
			transactionWriteRepository: _transactionWriteRepository,
			transferReadRepository: _transferReadRepository,
			transferWriteRepository: _transferWriteRepository,
			accountRepository: _accountRepository,
			currencyRateReadRepository: _currencyRateReadRepository,
			unitOfWork: _unitOfWork,
			dateProvider: FakeDateProvider.Default,
			options: new FakeOptionsMonitor<BalanceAdjustmentJobOptions>(value: DefaultOptions),
			logger: Substitute.For<ILogger<BalanceAdjustmentJob>>()
		);
	}

	private void SetupEmptyRepositories()
	{
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
	}

	private static PendingRateTransaction BuildTransaction(
		Guid? transactionId = null,
		Guid? accountId = null,
		decimal currentRate = 1m,
		string transactionCurrency = "USD",
		string baseCurrency = "RUB",
		int rowVersion = 0)
	{
		return new PendingRateTransaction(
			TransactionId: transactionId ?? Guid.CreateVersion7(),
			AccountId: accountId ?? Guid.CreateVersion7(),
			TransactionCurrency: Currency.Create(value: transactionCurrency).Value,
			BaseCurrency: Currency.Create(value: baseCurrency).Value,
			OccurredAt: FakeDateProvider.Default.UtcNow,
			CurrentRate: currentRate,
			Direction: DirectionType.Debit,
			RowVersion: rowVersion,
			Amount: 1000m
		);
	}

	private static PendingRateTransfer BuildTransfer(
		Guid? transferId = null,
		Guid? fromAccountId = null,
		Guid? toAccountId = null,
		Currency? currencyFrom = null,
		Currency? currencyTo = null,
		decimal amountFrom = 1000m,
		decimal currentRate = 1m,
		int rowVersion = 0)
	{
		return new PendingRateTransfer(
			TransferId: transferId ?? Guid.CreateVersion7(),
			FromAccountId: fromAccountId ?? Guid.CreateVersion7(),
			ToAccountId: toAccountId ?? Guid.CreateVersion7(),
			CurrencyFrom: currencyFrom ?? Currency.Reconstitute(value: "USD"),
			CurrencyTo: currencyTo ?? Currency.Reconstitute(value: "RUB"),
			OccurredAt: FakeDateProvider.Default.UtcNow,
			CurrentRate: currentRate,
			RowVersion: rowVersion,
			AmountFrom: amountFrom
		);
	}

	[Test]
	public async Task Execute_WhenNoPendingTransactions_ShouldNotFetchRate()
	{
		await _job.Execute(context: _jobContext);

		await _currencyRateReadRepository.DidNotReceive().GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTransactionRateNotFound_ShouldNotLoadAccount()
	{
		PendingRateTransaction transaction = BuildTransaction();
		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transaction]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (decimal?)null);

		await _job.Execute(context: _jobContext);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTransactionRateUnchanged_ShouldOnlyUpdateRate()
	{
		Guid accountId = Guid.CreateVersion7();
		PendingRateTransaction transaction = BuildTransaction(accountId: accountId, currentRate: 90m, rowVersion: 0);
		Account account = AccountFactory.Create(balance: 5000m).Value!;

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transaction]);

		_accountRepository.GetByIdAsync(accountId: accountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: account);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _job.Execute(context: _jobContext);

		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).UpdateRateAsync(
			transactionId: transaction.TransactionId,
			newRate: 90m,
			expectedVersion: 0,
			ct: Arg.Any<CancellationToken>()
		);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTransactionRateChanged_ShouldAdjustAccountBalance()
	{
		Guid accountId = Guid.CreateVersion7();
		PendingRateTransaction transaction = BuildTransaction(accountId: accountId, currentRate: 80m);
		Account account = AccountFactory.Create(balance: 5000m).Value!;

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transaction]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		_accountRepository.GetByIdAsync(accountId: accountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: account);

		await _job.Execute(context: _jobContext);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: account,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTransactionRateChanged_ShouldUpdateTransactionRate()
	{
		Guid transactionId = Guid.CreateVersion7();
		PendingRateTransaction transaction = BuildTransaction(transactionId: transactionId, currentRate: 80m, rowVersion: 0);
		Account account = AccountFactory.Create(balance: 5000m).Value!;

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transaction]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		_accountRepository.GetByIdAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: account);

		await _job.Execute(context: _jobContext);

		await _transactionWriteRepository.Received(requiredNumberOfCalls: 1).UpdateRateAsync(
			transactionId: transactionId,
			newRate: 90m,
			expectedVersion: 0,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenAccountNotFound_ShouldNotSave()
	{
		PendingRateTransaction transaction = BuildTransaction(currentRate: 80m);

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transaction]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		_accountRepository.GetByIdAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: (Account?)null);

		await _job.Execute(context: _jobContext);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenSaveThrowsUnexpectedException_ShouldContinueProcessingNextTransaction()
	{
		Guid firstAccountId = Guid.CreateVersion7();
		Guid secondAccountId = Guid.CreateVersion7();

		PendingRateTransaction first = BuildTransaction(accountId: firstAccountId, currentRate: 80m);
		PendingRateTransaction second = BuildTransaction(accountId: secondAccountId, currentRate: 80m);
		Account firstAccount = AccountFactory.Create(balance: 5000m).Value!;
		Account secondAccount = AccountFactory.Create(balance: 5000m).Value!;

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [first, second]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		_accountRepository.GetByIdAsync(
			accountId: firstAccountId,
			ct: Arg.Any<CancellationToken>()
		).Throws(createException: _ => throw new InvalidOperationException(message: "Database error"));

		_accountRepository.GetByIdAsync(
		   accountId: secondAccountId,
		   ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: secondAccount);

		await _job.Execute(context: _jobContext);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: secondAccount,
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenSaveThrowsNonConcurrencyExceptionForSameAccount_ShouldNotCarryStaleEventToNextTransaction()
	{
		Guid accountId = Guid.CreateVersion7();

		PendingRateTransaction first = BuildTransaction(accountId: accountId, currentRate: 80m);
		PendingRateTransaction second = BuildTransaction(accountId: accountId, currentRate: 80m);

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [first, second]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		_accountRepository.GetByIdAsync(
			accountId: accountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ => AccountFactory.CreateWithArchivation(balance: 5000m));

		List<Account> savedAccounts = [];
		int saveCallCount = 0;
		_accountRepository.SaveAsync(
			account: Arg.Do<Account>(useArgument: savedAccounts.Add),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			if (++saveCallCount == 1)
				throw new InvalidOperationException(message: "Database error");
			return Task.CompletedTask;
		});

		await _job.Execute(context: _jobContext);

		await Assert.That(value: saveCallCount).IsEqualTo(expected: 2);
		await Assert.That(value: savedAccounts).Count().IsEqualTo(expected: 2);

		Account secondSavedAccount = savedAccounts[1];
		await Assert.That(value: secondSavedAccount.Events.Count).IsEqualTo(expected: 1);
		await Assert.That(value: secondSavedAccount.Balance.Amount).IsEqualTo(expected: -5000m);
	}

	[Test]
	public async Task Execute_WhenConcurrencyConflict_ShouldRetryAndSucceed()
	{
		PendingRateTransaction transaction = BuildTransaction(currentRate: 80m);
		Account account = AccountFactory.Create(balance: 5000m).Value!;

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transaction]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		_accountRepository.GetByIdAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: account);

		int saveCallCount = 0;
		_accountRepository.SaveAsync(account: Arg.Any<Account>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
		{
			if (++saveCallCount == 1)
				throw new ConcurrencyConflictException(message: "Conflict.", id: Guid.CreateVersion7());
			return Task.CompletedTask;
		});

		await _job.Execute(context: _jobContext);

		await Assert.That(value: saveCallCount).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Execute_WhenCancelled_ShouldStopProcessingTransactions()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();
		_jobContext.CancellationToken.Returns(returnThis: cts.Token);

		PendingRateTransaction first = BuildTransaction(currentRate: 80m);
		PendingRateTransaction second = BuildTransaction(currentRate: 80m);

		_transactionReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [first, second]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: _ =>
		{
			cts.Cancel();
			return 90m;
		});

		_accountRepository.GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: AccountFactory.Create(balance: 5000m).Value!);

		await _job.Execute(context: _jobContext);

		await _accountRepository.Received(requiredNumberOfCalls: 1).GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenNoPendingTransfers_ShouldNotFetchTransferRate()
	{
		await _job.Execute(context: _jobContext);

		await _currencyRateReadRepository.DidNotReceive().GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTransferRateNotFound_ShouldNotLoadAccounts()
	{
		PendingRateTransfer transfer = BuildTransfer();

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transfer]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (decimal?)null);

		await _job.Execute(context: _jobContext);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenTransferRateUnchanged_ShouldOnlyUpdateTransferRate()
	{
		PendingRateTransfer transfer = BuildTransfer(currentRate: 90m, rowVersion: 0);

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transfer]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _job.Execute(context: _jobContext);

		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).UpdateRateAsync(
			transferId: transfer.TransferId,
			newRate: 90m,
			expectedVersion: 0,
			ct: Arg.Any<CancellationToken>()
		);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_WhenToAccountNotFound_ShouldNotSaveAnyAccount()
	{
		Guid fromAccountId = Guid.CreateVersion7();
		Guid toAccountId = Guid.CreateVersion7();
		PendingRateTransfer transfer = BuildTransfer(fromAccountId: fromAccountId, toAccountId: toAccountId, currentRate: 80m);

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transfer]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		_accountRepository.GetByIdAsync(accountId: fromAccountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: AccountFactory.Create().Value!);
		_accountRepository.GetByIdAsync(accountId: toAccountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: (Account?)null);

		await _job.Execute(context: _jobContext);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ProcessTransfers_CrossCurrency_ShouldAdjustOnlyToAccount()
	{
		Account toAccount = AccountFactory.Create(currency: "RUB", balance: 8000m).Value!;

		Guid fromAccountId = Guid.CreateVersion7();
		Guid toAccountId = toAccount.Id;

		PendingRateTransfer transfer = BuildTransfer(
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amountFrom: 100m,
			currencyFrom: Currency.Create(value: "USD").Value,
			currencyTo: Currency.Create(value: "RUB").Value,
			currentRate: 80m
		);

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transfer]);

		_accountRepository.GetByIdAsync(
			accountId: toAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: toAccount);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _job.Execute(_jobContext);

		await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
			account: Arg.Is<Account>(a => a!.Id == toAccountId && a.Balance.Amount == 9000m),
			ct: Arg.Any<CancellationToken>()
		);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: fromAccountId,
			ct: Arg.Any<CancellationToken>()
		);
		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Is<Account>(a => a!.Id == fromAccountId),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ProcessTransfers_CrossCurrency_WhenToAccountNotFound_ShouldSkip()
	{
		Guid fromAccountId = Guid.CreateVersion7();
		Guid toAccountId = Guid.CreateVersion7();

		PendingRateTransfer transfer = BuildTransfer(
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amountFrom: 100m,
			currencyFrom: Currency.Create(value: "USD").Value,
			currencyTo: Currency.Create(value: "RUB").Value,
			currentRate: 80m
		);

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transfer]);

		_accountRepository.GetByIdAsync(
			accountId: toAccountId,
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: (Account?)null);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(90m);

		await _job.Execute(_jobContext);

		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task ProcessTransfers_CrossCurrency_WhenRateUnchanged_ShouldOnlyUpdateRateWithoutSavingAccount()
	{
		Guid fromAccountId = Guid.CreateVersion7();
		Guid toAccountId = Guid.CreateVersion7();

		PendingRateTransfer transfer = BuildTransfer(
			fromAccountId: fromAccountId,
			toAccountId: toAccountId,
			amountFrom: 100m,
			currencyFrom: Currency.Create(value: "USD").Value,
			currencyTo: Currency.Create(value: "RUB").Value,
			currentRate: 90m
		);

		_transferReadRepository.GetPendingRateAsync(
			batchSize: Arg.Any<int>(),
			cursorOccurredAt: Arg.Any<DateTimeOffset?>(),
			cursorId: Arg.Any<Guid?>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: [transfer]);

		_currencyRateReadRepository.GetRateAsync(
			baseCurrencyCode: Arg.Any<Currency>(),
			targetCurrencyCode: Arg.Any<Currency>(),
			date: Arg.Any<DateOnly>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: 90m);

		await _job.Execute(_jobContext);

		await _accountRepository.DidNotReceive().GetByIdAsync(
			accountId: Arg.Any<Guid>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _accountRepository.DidNotReceive().SaveAsync(
			account: Arg.Any<Account>(),
			ct: Arg.Any<CancellationToken>()
		);
		await _transferWriteRepository.Received(requiredNumberOfCalls: 1).UpdateRateAsync(
			transferId: transfer.TransferId,
			newRate: 90m,
			expectedVersion: transfer.RowVersion,
			ct: Arg.Any<CancellationToken>()
		);
	}
}
