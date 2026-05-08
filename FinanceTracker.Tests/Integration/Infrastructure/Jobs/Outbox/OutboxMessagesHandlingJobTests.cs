using FinanceTracker.Application.UseCases.Accounts.Notifications;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Exceptions.DomainExceptions;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Jobs.Outbox;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.UnitOfWork;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using FinanceTracker.Infrastructure.Configurations.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Jobs.Outbox;

public sealed class OutboxMessagesHandlingJobTests : DatabaseFixture
{
    private static readonly IReadOnlyList<IAggregateNotificationFactory> Factories = [new AccountNotificationFactory()];

    private AccountRepository _accountRepository = null!;
    private INotificationDispatcher _dispatcher = null!;
    private CurrencyBuilder _currencyBuilder = null!;
    private AccountTypeBuilder _accountTypeBuilder = null!;
    private UserBuilder _userBuilder = null!;
    private OutboxMessagesHandlingJob _job = null!;

    [Before(hookType: Test)]
    public void Setup()
    {
        _dispatcher = Substitute.For<INotificationDispatcher>();
        _accountRepository = new AccountRepository(
            eventStore: new FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore(
                context: Context,
                eventTypeResolver: new EventTypeResolver(
                    assembly: typeof(IEvent).Assembly,
                    logger: Substitute.For<ILogger<EventTypeResolver>>()
                ),
                dateProvider: FakeDateProvider.Default,
                options: Options.Create(options: new EventStoreOptions()),
                logger: Substitute.For<ILogger<FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore>>()
            )
        );
        _currencyBuilder = new CurrencyBuilder(context: Context);
        _accountTypeBuilder = new AccountTypeBuilder(context: Context);
        _userBuilder = new UserBuilder(context: Context);

        _job = BuildJob();
    }

    private OutboxMessagesHandlingJob BuildJob(INotificationDispatcher? dispatcher = null)
    {
        return new OutboxMessagesHandlingJob(
            context: Context,
            dispatcher: dispatcher ?? _dispatcher,
            resolver: new EventTypeResolver(
                assembly: typeof(IEvent).Assembly,
                logger: Substitute.For<ILogger<EventTypeResolver>>()
            ),
            unitOfWork: new EFUnitOfWork(
                context: Context,
                logger: Substitute.For<ILogger<EFUnitOfWork>>()
            ),
            factories: Factories,
            dateProvider: FakeDateProvider.Default,
            options: Options.Create(options: new OutboxOptions()),
            logger: Substitute.For<ILogger<OutboxMessagesHandlingJob>>()
        );
    }

    private static INotificationDispatcher BuildFailingDispatcher()
    {
        INotificationDispatcher dispatcher = Substitute.For<INotificationDispatcher>();
        dispatcher.DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        ).Returns(returnThis: _ => Task.FromException(new InvalidOperationException(message: "Simulated dispatch failure")));
        return dispatcher;
    }

    private async Task CreateAndSaveAccountAsync()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        Result<Core.Domains.Account.Account, DomainException> result = Core.Domains.Account.Account.Create(
            occurredAt: FakeDateProvider.Default.UtcNow,
            userId: userId,
            name: "Карта Сбер",
            type: accountType,
            currency: Core.ValueObjects.Currency.Create(value: currencyCode).Value,
            balance: 1000m
        );
        Core.Domains.Account.Account account = result.Value!;
        await _accountRepository.SaveAsync(account: account);
    }

    [Test]
    public async Task ProcessBatchAsync_WhenNoMessages_ShouldNotDispatch()
    {
        await _job.ProcessMessagesAsync(ct: CancellationToken.None);

        await _dispatcher.DidNotReceive().DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task ProcessBatchAsync_ShouldDispatchNotificationAndMarkAsProcessed()
    {
        await CreateAndSaveAccountAsync();

        int unprocessedBefore = await Context.OutboxMessages
            .CountAsync(predicate: m => m.ProcessedAt == null);
        await Assert.That(value: unprocessedBefore).IsEqualTo(expected: 1);

        await _job.ProcessMessagesAsync(ct: CancellationToken.None);

        int unprocessedAfter = await Context.OutboxMessages
            .CountAsync(predicate: m => m.ProcessedAt == null);
        await Assert.That(value: unprocessedAfter).IsEqualTo(expected: 0);

        await _dispatcher.Received(requiredNumberOfCalls: 1).DispatchAsync(
            appNotification: Arg.Any<IAppNotification>(),
            ct: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task ProcessBatchAsync_WhenDispatcherAlwaysFails_ShouldMarkAsFailedAfterMaxRetries()
    {
        await CreateAndSaveAccountAsync();

        OutboxMessagesHandlingJob failingJob = BuildJob(dispatcher: BuildFailingDispatcher());

        for (int i = 0; i < 5; i++)
            await failingJob.ProcessMessagesAsync(ct: CancellationToken.None);

        OutboxMessageEntity? message = await Context.OutboxMessages
            .AsNoTracking()
            .FirstOrDefaultAsync();

        await Assert.That(value: message).IsNotNull();
        await Assert.That(value: message!.ProcessedAt).IsNull();
        await Assert.That(value: message.RetryCount).IsEqualTo(expected: 5);
        await Assert.That(value: message.FailedAt).IsNotNull();
    }

    [Test]
    public async Task ProcessBatchAsync_WhenMessageIsInDeadLetter_ShouldNotRetryIt()
    {
        await CreateAndSaveAccountAsync();

        OutboxMessagesHandlingJob failingJob = BuildJob(dispatcher: BuildFailingDispatcher());

        for (int i = 0; i < 5; i++)
            await failingJob.ProcessMessagesAsync(ct: CancellationToken.None);

        await failingJob.ProcessMessagesAsync(ct: CancellationToken.None);

        OutboxMessageEntity? message = await Context.OutboxMessages
            .AsNoTracking()
            .FirstOrDefaultAsync();

        await Assert.That(value: message!.RetryCount).IsEqualTo(expected: 5);
    }
    
    [Test]
    public async Task ProcessBatchAsync_WhenDispatcherFailsThenSucceeds_ShouldNotMarkAsProcessedOnFailureAndSucceedOnRetry()
    {
        await CreateAndSaveAccountAsync();

        OutboxMessagesHandlingJob failingJob = BuildJob(dispatcher: BuildFailingDispatcher());
        await failingJob.ProcessMessagesAsync(ct: CancellationToken.None);

        OutboxMessageEntity? messageAfterFailure = await Context.OutboxMessages.AsNoTracking().FirstOrDefaultAsync();

        await Assert.That(value: messageAfterFailure!.ProcessedAt).IsNull();
        await Assert.That(value: messageAfterFailure.RetryCount).IsEqualTo(expected: 1);

        await _job.ProcessMessagesAsync(ct: CancellationToken.None);

        OutboxMessageEntity? messageAfterSuccess = await Context.OutboxMessages.AsNoTracking().FirstOrDefaultAsync();

        await Assert.That(value: messageAfterSuccess!.ProcessedAt).IsNotNull();
        await Assert.That(value: messageAfterSuccess.RetryCount).IsEqualTo(expected: 1);
    }
}