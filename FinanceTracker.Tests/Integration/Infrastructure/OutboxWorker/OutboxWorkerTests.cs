using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Dispatching;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Core.Repositories;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.Database.UOW;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FinanceTracker.Tests.Integration.Infrastructure.OutboxWorker;

public sealed class OutboxWorkerTests : DatabaseFixture
{
    private static readonly IReadOnlyList<IAggregateNotificationFactory> Factories = [new AccountNotificationFactory()];
	
    private AccountRepository _accountRepository = null!;
    private IPublisher _publisher = null!;
    private CurrencyBuilder _currencyBuilder = null!;
    private AccountTypeBuilder _accountTypeBuilder = null!;
    private UserBuilder _userBuilder = null!;
    
    [Before(hookType: Test)]
    public void SetupRepositories()
    {
        _publisher = Substitute.For<IPublisher>();
        _accountRepository = new AccountRepository(
            eventStore: new FinanceTracker.Infrastructure.Database.EventStore.PostgresEventStore(
                context: Context,
                eventTypeResolver: new EventTypeResolver(assembly: typeof(IEvent).Assembly)
            )
        );
        _currencyBuilder = new CurrencyBuilder(context: Context);
        _accountTypeBuilder = new AccountTypeBuilder(context: Context);
        _userBuilder = new UserBuilder(context: Context);
    }

    private IServiceProvider BuildScope()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<FinanceTrackerContext>(implementationInstance: Context);
        services.AddSingleton<IEventTypeResolver>(implementationInstance:
            new EventTypeResolver(assembly: typeof(IEvent).Assembly)
        );
        services.AddSingleton<IPublisher>(implementationInstance: _publisher);
        services.AddScoped<INotificationDispatcher, MediatRNotificationDispatcher>();
        services.AddScoped<IUnitOfWork, EFUnitOfWork>();
        return services.BuildServiceProvider();
    }

    private async Task<Core.Domains.Account.Account> CreateAndSaveAccountAsync()
    {
        string currencyCode = await _currencyBuilder.CreateAsync();
        Core.Domains.Account.AccountType accountType = await _accountTypeBuilder.CreateAsync();
        Guid userId = await _userBuilder.CreateAsync(currencyCode: currencyCode);

        Core.Domains.Account.Account account = Core.Domains.Account.Account.Create(
            userId: userId,
            name: "Карта Сбер",
            type: accountType,
            currency: currencyCode,
            balance: 1000m
        );

        await _accountRepository.SaveAsync(account: account);
        return account;
    }   

    [Test]
    public async Task ProcessBatchAsync_ShouldDispatchNotificationAndMarkAsProcessed()
    {
        _ = await CreateAndSaveAccountAsync();

        int unprocessedBefore = await Context.OutboxMessages
            .CountAsync(predicate: m => m.ProcessedAt == null);
        await Assert.That(value: unprocessedBefore).IsEqualTo(expected: 1);

        FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker worker = new FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker(
            scopeFactory: new FakeScopeFactory(serviceProvider: BuildScope()),
            logger: NullLogger<FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker>.Instance,
            factories: Factories
        );

        await worker.ProcessBatchAsync(ct: CancellationToken.None);

        int unprocessedAfter = await Context.OutboxMessages.CountAsync(predicate: m => m.ProcessedAt == null);
        await Assert.That(value: unprocessedAfter).IsEqualTo(expected: 0);

        await _publisher.Received(requiredNumberOfCalls: 1).Publish(
            notification: Arg.Is<AccountEventsNotification>(predicate: n => n.Events.Count == 1),
            cancellationToken: Arg.Any<CancellationToken>()
        );
    }

    [Test]
    public async Task ProcessBatchAsync_WhenNoMessages_ShouldNotDispatch()
    {
        FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker worker = new FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker(
            scopeFactory: new FakeScopeFactory(serviceProvider: BuildScope()),
            logger: NullLogger<FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker>.Instance,
            factories: Factories
        );

        await worker.ProcessBatchAsync(ct: CancellationToken.None);

        await _publisher.DidNotReceive().Publish(
            notification: Arg.Any<INotification>(),
            cancellationToken: Arg.Any<CancellationToken>()
        );
    }
    
    [Test]
    public async Task ProcessBatchAsync_WhenDispatcherAlwaysFails_ShouldMarkAsFailedAfterMaxRetries()
    {
        _ = await CreateAndSaveAccountAsync();

        _publisher.Publish(notification: Arg.Any<INotification>(), cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(ex: new InvalidOperationException("Simulated dispatch failure"));

        FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker worker = new FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker(
            scopeFactory: new FakeScopeFactory(serviceProvider: BuildScope()),
            logger: NullLogger<FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker>.Instance,
            factories: Factories
        );

        for (int i = 0; i < 5; i++)
            await worker.ProcessBatchAsync(ct: CancellationToken.None);

        OutboxMessageEntity? message = await Context.OutboxMessages.FirstOrDefaultAsync();

        await Assert.That(value: message).IsNotNull();
        await Assert.That(value: message.ProcessedAt).IsNull();
        await Assert.That(value: message.RetryCount).IsEqualTo(expected: 5);
        await Assert.That(value: message.FailedAt).IsNotNull();
    }

    [Test]
    public async Task ProcessBatchAsync_WhenMessageIsInDeadLetter_ShouldNotRetryIt()
    {
        _ = await CreateAndSaveAccountAsync();

        _publisher
        .Publish(notification: Arg.Any<INotification>(), cancellationToken: Arg.Any<CancellationToken>())
            .ThrowsAsync(ex: new InvalidOperationException(message: "Simulated dispatch failure"));

        FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker worker = new FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker(
            scopeFactory: new FakeScopeFactory(serviceProvider: BuildScope()),
            logger: NullLogger<FinanceTracker.Infrastructure.Database.Workers.Outbox.OutboxWorker>.Instance,
            factories: Factories
        );

        for (int i = 0; i < 5; i++)
            await worker.ProcessBatchAsync(ct: CancellationToken.None);

        await worker.ProcessBatchAsync(ct: CancellationToken.None);

        OutboxMessageEntity? message = await Context.OutboxMessages.FirstOrDefaultAsync();

        await Assert.That(value: message!.RetryCount).IsEqualTo(expected: 5);
    }
}