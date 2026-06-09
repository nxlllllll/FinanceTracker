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
        UseJitter = false
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
        ).Returns(returnThis: call => call.Arg<Func<Task>>()());

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
        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: []);
        _transferReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: []);
    }

    private static PendingRateTransaction BuildTransaction(
        Guid? transactionId = null,
        Guid? accountId = null,
        decimal currentRate = 1m,
        string transactionCurrency = "USD",
        string baseCurrency = "RUB")
    {
        return new PendingRateTransaction(
            TransactionId: transactionId ?? Guid.CreateVersion7(),
            AccountId: accountId ?? Guid.CreateVersion7(),
            TransactionCurrency: Currency.Create(value: transactionCurrency).Value,
            BaseCurrency: Currency.Create(value: baseCurrency).Value,
            OccurredAt: FakeDateProvider.Default.UtcNow,
            CurrentRate: currentRate,
            Direction: DirectionType.Debit,
            Amount: 1000m
        );
    }

    private static PendingRateTransfer BuildTransfer(
        Guid? transferId = null,
        Guid? fromAccountId = null,
        Guid? toAccountId = null,
        decimal currentRate = 1m)
    {
        return new PendingRateTransfer(
            TransferId: transferId ?? Guid.CreateVersion7(),
            FromAccountId: fromAccountId ?? Guid.CreateVersion7(),
            ToAccountId: toAccountId ?? Guid.CreateVersion7(),
            CurrencyFrom: Currency.Reconstitute(value: "USD"),
            CurrencyTo: Currency.Reconstitute(value: "RUB"),
            OccurredAt: FakeDateProvider.Default.UtcNow,
            CurrentRate: currentRate,
            AmountFrom: 1000m
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

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transaction]);

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
    public async Task Execute_WhenTransactionRateUnchanged_ShouldOnlyUpdateRate()
    {
        PendingRateTransaction transaction = BuildTransaction(currentRate: 90m);

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transaction]);

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
            ct: Arg.Any<CancellationToken>()
        );

        await _accountRepository.DidNotReceive().GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenTransactionRateChanged_ShouldAdjustAccountBalance()
    {
        Guid accountId = Guid.CreateVersion7();
        PendingRateTransaction transaction = BuildTransaction(accountId: accountId, currentRate: 80m);
        Account account = AccountFactory.Create(balance: 5000m).Value!;

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transaction]);

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
        PendingRateTransaction transaction = BuildTransaction(transactionId: transactionId, currentRate: 80m);
        Account account = AccountFactory.Create(balance: 5000m).Value!;

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transaction]);

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
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenAccountNotFound_ShouldNotSave()
    {
        PendingRateTransaction transaction = BuildTransaction(currentRate: 80m);

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transaction]);

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
        PendingRateTransaction first = BuildTransaction(currentRate: 80m);
        PendingRateTransaction second = BuildTransaction(currentRate: 80m);
        Account account = AccountFactory.Create(balance: 5000m).Value!;

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [first, second]);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: Arg.Any<Currency>(),
            targetCurrencyCode: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 90m);

        int callCount = 0;
        _accountRepository.GetByIdAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: _ =>
        {
            ++callCount;
            if (callCount == 1)
                throw new InvalidOperationException(message: "Database error");
            return account;
        });

        await _job.Execute(context: _jobContext);

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: account,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenConcurrencyConflict_ShouldRetryAndSucceed()
    {
        PendingRateTransaction transaction = BuildTransaction(currentRate: 80m);
        Account account = AccountFactory.Create(balance: 5000m).Value!;

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transaction]);

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

        _transactionReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [first, second]);

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

        _accountRepository.GetByIdAsync(accountId: Arg.Any<Guid>(), ct: Arg.Any<CancellationToken>()).Returns(returnThis: AccountFactory.Create(balance: 5000m).Value!);

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

        _transferReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transfer]);

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
        PendingRateTransfer transfer = BuildTransfer(currentRate: 90m);

        _transferReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transfer]);

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
            ct: Arg.Any<CancellationToken>()
        );

        await _accountRepository.DidNotReceive().GetByIdAsync(
            accountId: Arg.Any<Guid>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenTransferRateChanged_ShouldAdjustBothAccounts()
    {
        Guid fromAccountId = Guid.CreateVersion7();
        Guid toAccountId = Guid.CreateVersion7();
        PendingRateTransfer transfer = BuildTransfer(
            fromAccountId: fromAccountId,
            toAccountId: toAccountId,
            currentRate: 80m
        );

        Account fromAccount = AccountFactory.Create(balance: 5000m).Value!;
        Account toAccount = AccountFactory.Create(balance: 5000m).Value!;

        _transferReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transfer]);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: Arg.Any<Currency>(),
            targetCurrencyCode: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 90m);

        _accountRepository.GetByIdAsync(accountId: fromAccountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: fromAccount);

        _accountRepository.GetByIdAsync(accountId: toAccountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: toAccount);

        await _job.Execute(context: _jobContext);

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: fromAccount,
            ct: Arg.Any<CancellationToken>()
        );

        await _accountRepository.Received(requiredNumberOfCalls: 1).SaveAsync(
            account: toAccount,
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenFromAccountNotFound_ShouldNotSaveAnyAccount()
    {
        Guid fromAccountId = Guid.CreateVersion7();
        Guid toAccountId = Guid.CreateVersion7();
        PendingRateTransfer transfer = BuildTransfer(
            fromAccountId: fromAccountId,
            toAccountId: toAccountId,
            currentRate: 80m
        );

        _transferReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transfer]);

        _currencyRateReadRepository.GetRateAsync(
            baseCurrencyCode: Arg.Any<Currency>(),
            targetCurrencyCode: Arg.Any<Currency>(),
            date: Arg.Any<DateOnly>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: 90m);

        _accountRepository.GetByIdAsync(accountId: fromAccountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: (Account?)null);

        _accountRepository.GetByIdAsync(accountId: toAccountId, ct: Arg.Any<CancellationToken>()).Returns(returnThis: AccountFactory.Create().Value!);

        await _job.Execute(context: _jobContext);

        await _accountRepository.DidNotReceive().SaveAsync(
            account: Arg.Any<Account>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task Execute_WhenToAccountNotFound_ShouldNotSaveAnyAccount()
    {
        Guid fromAccountId = Guid.CreateVersion7();
        Guid toAccountId = Guid.CreateVersion7();
        PendingRateTransfer transfer = BuildTransfer(
            fromAccountId: fromAccountId,
            toAccountId: toAccountId,
            currentRate: 80m
        );

        _transferReadRepository.GetPendingRateAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: [transfer]);

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
}
