using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Services.EventStore;
using FinanceTracker.Core.Services.Rebuild;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Infrastructure.Services.Rebuild;
using FinanceTracker.Infrastructure.Services.Rebuild.Account;
using FinanceTracker.Tests.Integration._Shared.Builders;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Services.Rebuild;

public sealed class ProjectionRebuilderTests : DatabaseFixture
{
	private AccountRepository _accountRepository = null!;
	private AccountWriteRepository _writeRepository = null!;
	private AccountProjectionRebuild _projection = null!;
	private ProjectionRebuilder _rebuilder = null!;
	private UserBuilder _userBuilder = null!;

	private PostgresEventStore CreateEventStore() => new PostgresEventStore(
		context: Context,
		eventTypeResolver: new EventTypeResolver(
			assembly: typeof(IEvent).Assembly,
			logger: Substitute.For<ILogger<EventTypeResolver>>()
		),
		integrationEventMapper: new AccountIntegrationEventMapper(),
		integrationEventTypeResolver: new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IIntegrationEvent).Assembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		),
		dateProvider: FakeDateProvider.Default,
		correlationContext: Substitute.For<ICorrelationContext>(),
		upcasterRegistry: CreatePassthroughUpcasterRegistry(),
		options: new FakeOptionsMonitor<EventStoreOptions>(value: new EventStoreOptions()),
		logger: Substitute.For<ILogger<PostgresEventStore>>(),
		eventSchemaHealthState: Substitute.For<IEventSchemaHealthState>()
	);

	private static IEventUpcasterRegistry CreatePassthroughUpcasterRegistry()
	{
		IEventUpcasterRegistry registry = Substitute.For<IEventUpcasterRegistry>();
		registry.HasChain(eventType: Arg.Any<string>()).Returns(returnThis: false);
		return registry;
	}

	[Before(hookType: Test)]
	public void SetupRepositories()
	{
		PostgresEventStore eventStore = CreateEventStore();

		_accountRepository = new AccountRepository(
			eventStore: eventStore,
			snapshotSerializer: new AccountSnapshotSerializer(),
			unitOfWork: UnitOfWork
		);
		_writeRepository = new AccountWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);
		_projection = new AccountProjectionRebuild(
			repository: _writeRepository,
			applier: new AccountDomainEventApplier(repository: _writeRepository)
		);
		_rebuilder = new ProjectionRebuilder(
			eventStore: eventStore,
			unitOfWork: UnitOfWork,
			scopeFactory: Substitute.For<IServiceScopeFactory>(),
			logger: Substitute.For<ILogger<ProjectionRebuilder>>()
		);
		_userBuilder = new UserBuilder(context: Context);
	}

	private Task RebuildAsync(Guid accountId) => _rebuilder.RebuildAsync(
		projection: _projection,
		aggregateType: AggregateTypeNames.Account,
		aggregateId: accountId,
		ct: CancellationToken.None
	);

	private async Task<(Account Account, decimal Balance)> CreateProjectedAccountAsync(int debits)
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: "RUB");
		Account account = AccountFactory.Create(userId: userId, balance: 10_000m, currency: "RUB").Value!;

		AccountDomainEventApplier applier = new AccountDomainEventApplier(repository: _writeRepository);

		async Task SaveAndProjectAsync(Account aggregate)
		{
			List<IEvent> events = aggregate.Events.ToList();

			await UnitOfWork.ExecuteInTransactionAsync(
				operation: async () => await _accountRepository.SaveAsync(account: aggregate, ct: CancellationToken.None)
			);

			foreach (IEvent @event in events)
				await applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

			await Context.SaveChangesAsync();
			Context.ChangeTracker.Clear();
		}

		await SaveAndProjectAsync(aggregate: account);

		Account loaded = (await _accountRepository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None))!;

		for (int i = 0; i < debits; i++)
		{
			loaded.Debit(
				occurredAt: FakeDateProvider.Default.UtcNow,
				transactionId: Guid.CreateVersion7(),
				categoryId: Guid.CreateVersion7(),
				amount: 100m,
				exchangeRate: 1m,
				description: null
			);
			await SaveAndProjectAsync(aggregate: loaded);
		}

		return (loaded, await GetBalanceAsync(accountId: account.Id));
	}

	private async Task<decimal> GetBalanceAsync(Guid accountId)
	{
		return await Context.AccountBalances.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();
	}

	private async Task<int> GetVersionAsync(Guid accountId)
	{
		return await Context.Accounts.Where(predicate: a => a.Id == accountId)
			.Select(selector: a => a.LastVersion)
			.FirstAsync();
	}

	[Test]
	public async Task RebuildAsync_ShouldRestoreABalanceThatWasTamperedWith()
	{
		(Account account, decimal balance) = await CreateProjectedAccountAsync(debits: 3);

		await Context.AccountBalances.Where(predicate: b => b.AccountId == account.Id).ExecuteUpdateAsync(
			setPropertyCalls: s => s.SetProperty(propertyExpression: b => b.Balance, valueExpression: 999_999m)
		);
		Context.ChangeTracker.Clear();

		await RebuildAsync(accountId: account.Id);

		await Assert.That(value: await GetBalanceAsync(accountId: account.Id)).IsEqualTo(expected: balance);
	}

	[Test]
	public async Task RebuildAsync_ShouldRestoreTheAggregateVersion()
	{
		(Account account, _) = await CreateProjectedAccountAsync(debits: 3);
		int version = await GetVersionAsync(accountId: account.Id);

		await Context.Accounts.Where(predicate: a => a.Id == account.Id).ExecuteUpdateAsync(
			setPropertyCalls: s => s.SetProperty(propertyExpression: a => a.LastVersion, valueExpression: 1)
		);
		Context.ChangeTracker.Clear();

		await RebuildAsync(accountId: account.Id);

		await Assert.That(value: await GetVersionAsync(accountId: account.Id)).IsEqualTo(expected: version).Because(message: """
			A rebuild that restores the balance and leaves the version behind is still broken: accounts.last_version
			is the counter behind both the ETag and the If-Match check, so the account would come back with correct
			money and no way to write to it conditionally.
		""");
	}

	[Test]
	public async Task RebuildAsync_ShouldReplayTheWholeHistoryPastASnapshot()
	{
		(Account account, decimal balance) = await CreateProjectedAccountAsync(debits: 30);

		await Context.AccountBalances.Where(predicate: b => b.AccountId == account.Id).ExecuteUpdateAsync(
			setPropertyCalls: s => s.SetProperty(propertyExpression: b => b.Balance, valueExpression: 0m)
		);
		Context.ChangeTracker.Clear();

		await RebuildAsync(accountId: account.Id);

		await Assert.That(value: await GetBalanceAsync(accountId: account.Id)).IsEqualTo(expected: balance);
		await Assert.That(value: balance).IsEqualTo(expected: 7_000m);
	}

	[Test]
	public async Task RebuildAsync_ShouldBeIdempotent()
	{
		(Account account, decimal balance) = await CreateProjectedAccountAsync(debits: 3);

		await RebuildAsync(accountId: account.Id);
		await RebuildAsync(accountId: account.Id);

		await Assert.That(value: await GetBalanceAsync(accountId: account.Id)).IsEqualTo(expected: balance).Because(message: """
			Erasing first is what makes this safe to run twice. Replaying onto rows that already hold the same
			history would double every delta, and an operator who is not sure the first run finished will run it
			again.
		""");
	}

	[Test]
	public async Task RebuildAsync_ForAnAggregateWithNoEvents_ShouldLeaveTheProjectionAlone()
	{
		(Account account, decimal balance) = await CreateProjectedAccountAsync(debits: 1);

		await RebuildAsync(accountId: Guid.CreateVersion7());

		await Assert.That(value: await GetBalanceAsync(accountId: account.Id)).IsEqualTo(expected: balance).Because(message: """
			An empty log is not evidence that the projection should be empty — it is far more likely to be a
			mistyped id. Erasing on that basis would turn a typo into data loss.
		""");
	}
}
