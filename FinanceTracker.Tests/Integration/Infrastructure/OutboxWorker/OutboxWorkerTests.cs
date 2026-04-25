using FinanceTracker.Application.Accounts.Notifications;
using FinanceTracker.Application.Dispatching;
using FinanceTracker.Core.Domains.Abstractions;
using FinanceTracker.Infrastructure.Database;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using FinanceTracker.Tests.Integration.Infrastructure._Shared.Builders;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.OutboxWorker;

public sealed class OutboxWorkerTests : DatabaseFixture
{
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

    private IServiceScope BuildScope()
    {
        ServiceCollection services = new ServiceCollection();
        services.AddSingleton<FinanceTrackerContext>(implementationInstance: Context);
        services.AddSingleton<IEventTypeResolver>(implementationInstance:
            new EventTypeResolver(assembly: typeof(IEvent).Assembly)
        );
        services.AddSingleton<IPublisher>(implementationInstance: _publisher);
        services.AddScoped<INotificationDispatcher, MediatRNotificationDispatcher>();
        return services.BuildServiceProvider().CreateScope();
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

        FinanceTracker.Infrastructure.Database.Outbox.OutboxWorker worker = new FinanceTracker.Infrastructure.Database.Outbox.OutboxWorker(
            scopeFactory: new FakeScopeFactory(scope: BuildScope()),
            logger: NullLogger<FinanceTracker.Infrastructure.Database.Outbox.OutboxWorker>.Instance
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
        FinanceTracker.Infrastructure.Database.Outbox.OutboxWorker worker = new FinanceTracker.Infrastructure.Database.Outbox.OutboxWorker(
            scopeFactory: new FakeScopeFactory(scope: BuildScope()),
            logger: NullLogger<FinanceTracker.Infrastructure.Database.Outbox.OutboxWorker>.Instance
        );

        await worker.ProcessBatchAsync(ct: CancellationToken.None);

        await _publisher.DidNotReceive().Publish(
            notification: Arg.Any<INotification>(),
            cancellationToken: Arg.Any<CancellationToken>()
        );
    }
}