using FinanceTracker.Application.UseCases.Account.Commands.CreateAccount;
using FinanceTracker.Application.UseCases.Transfer.Commands;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Domains.Transfer;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.Context.Account;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.E2E;

/// <summary>
/// E2E: CreateTransfer → outbox → RabbitMQ → AccountTransferConsumer → credit / compensation.
/// </summary>
public sealed class TransferProjectionE2ETests : E2EFixture
{
    private UserBuilder _userBuilder = null!;

    [Before(hookType: Test)]
    public async Task SetupDataAsync()
    {
        _userBuilder = new UserBuilder(context: Context);
        await new CurrencyBuilder(context: Context).CreateAsync(code: "RUB");
    }

    private async Task<Guid> CreateAccountViaCommandAsync(Guid userId, decimal balance)
    {
        Result<Guid, DomainException> result = await Mediator.Send(request: new CreateAccountCommand(
            UserId: userId,
            Name: Name.Create(value: "Счёт").Value,
            Type: AccountType.Checking,
            Currency: Currency.Create(value: "RUB").Value,
            InitialBalance: balance
        ) { IdempotencyKey = Guid.CreateVersion7() });

        Guid accountId = result.Value!;

        await RunOutboxAsync();
        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            return await ctx.Accounts.AnyAsync(predicate: a => a.Id == accountId);
        });

        return accountId;
    }

    [Test]
    public async Task CreateTransfer_AfterOutbox_ShouldCompleteAndUpdateBothBalances()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid fromAccountId = await CreateAccountViaCommandAsync(userId: userId, balance: 10_000m);
        Guid toAccountId = await CreateAccountViaCommandAsync(userId: userId, balance: 2_000m);

        await Mediator.Send(request: new CreateTransferCommand(
            UserId: userId,
            FromAccountId: fromAccountId,
            ToAccountId: toAccountId,
            Amount: 3_000m,
            Description: null,
            OccurredAt: DateTimeOffset.UtcNow
        ) { IdempotencyKey = Guid.CreateVersion7() });

        await RunOutboxAsync();

        Guid? transferId;
        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            transferId = await ctx.Transfers.Where(predicate: t => t.FromAccountId == fromAccountId && t.Status == TransferStatus.Completed)
                .Select(selector: t => t.Id)
                .FirstOrDefaultAsync();
            return transferId.HasValue;
        });

        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            decimal? b = await ctx.AccountBalances.Where(predicate: x => x.AccountId == fromAccountId)
                .Select(selector: x => x.Balance)
                .FirstOrDefaultAsync();
            return b == 7_000m;
        });

        await RunOutboxAsync();
        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            decimal? b = await ctx.AccountBalances.Where(predicate: x => x.AccountId == toAccountId)
                .Select(selector: x => x.Balance)
                .FirstOrDefaultAsync();
            return b == 5_000m;
        });

        await using FinanceTrackerContext readCtx = CreateReadContext();

        decimal fromBalance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == fromAccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        decimal toBalance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == toAccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: fromBalance).IsEqualTo(expected: 7_000m);
        await Assert.That(value: toBalance).IsEqualTo(expected: 5_000m);
    }

    [Test]
    public async Task CreateTransfer_WhenToAccountDeleted_ShouldCompensateAndRefundFromAccount()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid fromAccountId = await CreateAccountViaCommandAsync(userId: userId, balance: 10_000m);

        Guid nonExistentToAccountId = Guid.CreateVersion7();

        await Context.Accounts.AddAsync(entity: new AccountEntity
        {
            Id = nonExistentToAccountId,
            UserId = userId,
            Name = Name.Create(value: "Temp").Value,
            AccountType = AccountType.Checking,
            Currency = Currency.Create(value: "RUB").Value,
            IsArchived = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await Context.AccountBalances.AddAsync(entity: new AccountBalanceEntity
        {
            AccountId = nonExistentToAccountId,
            Balance = 0m,
            LastVersion = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await Context.SaveChangesAsync();

        await Mediator.Send(request: new CreateTransferCommand(
            UserId: userId,
            FromAccountId: fromAccountId,
            ToAccountId: nonExistentToAccountId,
            Amount: 4_000m,
            Description: null,
            OccurredAt: DateTimeOffset.UtcNow
        ) { IdempotencyKey = Guid.CreateVersion7() });

        await RunOutboxAsync();

        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            return await ctx.Transfers.AnyAsync(predicate: t => t.FromAccountId == fromAccountId && t.Status == TransferStatus.Compensated);
        });

        await RunOutboxAsync();

        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            decimal? balance = await ctx.AccountBalances.Where(predicate: b => b.AccountId == fromAccountId)
                .Select(selector: b => b.Balance)
                .FirstOrDefaultAsync();
            return balance == 10_000m;
        });

        await using FinanceTrackerContext readCtx = CreateReadContext();
        decimal fromBalance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == fromAccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: fromBalance).IsEqualTo(expected: 10_000m);
    }

    [Test]
    public async Task CreateTransfer_StuckBeyondThreshold_TransferCreditLagShouldCompensate()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid fromAccountId = await CreateAccountViaCommandAsync(userId: userId, balance: 8_000m);

        TransferBuilder transferBuilder = new TransferBuilder(context: Context);

        // toAccount does not exist in ES — consumer compensates when trying to apply credit
        // But for the lag job test, we need a transfer to PendingCredit, which was created a long time ago.
        // We create directly through the builder and mark occurred_at in the past
        Guid toAccountId = await CreateAccountViaCommandAsync(userId: userId, balance: 0m);
        Guid transferId = await transferBuilder.CreateAsync(
            userId: userId,
            fromAccountId: fromAccountId,
            currencyFrom: "RUB",
            toAccountId: toAccountId,
            currencyTo: "RUB",
            amount: 2_000m,
            occurredAt: DateTimeOffset.UtcNow.AddMinutes(minutes: -60) // older than CompensationThreshold
        );

        await RunTransferCreditLagAsync();

        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            return await ctx.Transfers.AnyAsync(predicate: t => t.Id == transferId && t.Status == TransferStatus.Compensated);
        });

        await using FinanceTrackerContext readCtx = CreateReadContext();
        TransferStatus status = await readCtx.Transfers.Where(predicate: t => t.Id == transferId)
            .Select(selector: t => t.Status)
            .FirstAsync();

        await Assert.That(value: status).IsEqualTo(expected: TransferStatus.Compensated);
    }

    [Test]
    public async Task CreateTransfer_DuplicateMessage_ShouldNotProcessTwice()
    {
        Guid userId = await _userBuilder.CreateAsync();
        Guid fromAccountId = await CreateAccountViaCommandAsync(userId: userId, balance: 10_000m);
        Guid toAccountId = await CreateAccountViaCommandAsync(userId: userId, balance: 0m);

        await Mediator.Send(new CreateTransferCommand(
            UserId: userId,
            FromAccountId: fromAccountId,
            ToAccountId: toAccountId,
            Amount: 1_000m,
            Description: null,
            OccurredAt: DateTimeOffset.UtcNow
        ) { IdempotencyKey = Guid.CreateVersion7() });

        // We publish it twice — the outbox contains one message, but
        // idempotency via processed_messages protects against double processing
        await RunOutboxAsync();
        await RunOutboxAsync();

        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            return await ctx.Transfers.AnyAsync(predicate: t => t.FromAccountId == fromAccountId && t.Status == TransferStatus.Completed);
        });

        // Credit toAccount generates a new entry in the outbox AFTER we have published
        // the stack is higher — one more pass is needed for AccountEventsConsumer to project the balance.
        await RunOutboxAsync();
        await WaitForConditionAsync(condition: async () =>
        {
            await using FinanceTrackerContext ctx = CreateReadContext();
            decimal? b = await ctx.AccountBalances.Where(predicate: x => x.AccountId == toAccountId)
                .Select(selector: x => x.Balance)
                .FirstOrDefaultAsync();
            return b == 1_000m;
        });

        await using FinanceTrackerContext readCtx = CreateReadContext();
        decimal toBalance = await readCtx.AccountBalances.Where(predicate: b => b.AccountId == toAccountId)
            .Select(selector: b => b.Balance)
            .FirstAsync();

        await Assert.That(value: toBalance).IsEqualTo(expected: 1_000m);
    }
}