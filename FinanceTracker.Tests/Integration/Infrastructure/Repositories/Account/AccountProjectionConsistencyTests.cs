using FinanceTracker.Contracts.Events.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Observability.Correlation;
using FinanceTracker.Core.Services.EventStore;
using FinanceTracker.Core.ValueObjects;
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
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Account;

/// <summary>
/// Verifies that the event-sourced aggregate (full replay) and the incrementally-projected
/// read model (delta application) stay numerically identical through non-trivial,
/// multi-decimal exchange rate conversions — and that a full rebuild from the event store
/// reproduces exactly the same balance. This is the regression test for the rounding fix in
/// <see cref="Money.ConvertedAmount"/>: before that fix, the write side (which replays every
/// event with full decimal precision) and the read side (which applies each conversion as an
/// incremental SQL delta) could silently drift apart, a fraction of a cent at a time.
/// </summary>
public sealed class AccountProjectionConsistencyTests : DatabaseFixture
{
	private AccountRepository _accountRepository = null!;
	private AccountWriteRepository _writeRepository = null!;
	private AccountDomainEventApplier _applier = null!;
	private ProjectionRebuilder _rebuilder = null!;
	private AccountProjectionRebuild _projection = null!;
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
		_writeRepository = new AccountWriteRepository(
			context: Context,
			dateProvider: FakeDateProvider.Default
		);
		_applier = new AccountDomainEventApplier(repository: _writeRepository);
		_projection = new AccountProjectionRebuild(repository: _writeRepository, applier: _applier);
		_rebuilder = new ProjectionRebuilder(
			eventStore: eventStore,
			unitOfWork: UnitOfWork,
			scopeFactory: Substitute.For<IServiceScopeFactory>(),
			logger: Substitute.For<ILogger<ProjectionRebuilder>>()
		);
		_userBuilder = new UserBuilder(context: Context);
	}

	/// <summary>
	/// Creates a real <c>users</c> row (and, transitively, the <c>currencies</c> row it
	/// references) before building the account — <c>AccountWriteRepository.CreateAsync</c>
	/// inserts into the read-model <c>accounts</c> table, which has a foreign key to
	/// <c>users</c>, so a random unpersisted <c>UserId</c> from a bare in-memory aggregate
	/// would violate that constraint the moment the projection step runs.
	/// </summary>
	private async Task<Core.Domains.Account.Account> CreateTestAccountAsync(decimal balance, string currency)
	{
		Guid userId = await _userBuilder.CreateAsync(currencyCode: currency);
		return AccountFactory.Create(userId: userId, balance: balance, currency: currency).Value!;
	}

	private Task RebuildAsync(Guid accountId) => _rebuilder.RebuildAsync(
		projection: _projection,
		aggregateType: AggregateTypeNames.Account,
		aggregateId: accountId,
		ct: CancellationToken.None
	);

	/// <summary>
	/// Persists the account's pending events to the event store (source of truth) and then
	/// applies those same events to the read model — mirroring exactly what the outbox and
	/// projection consumer do in production, one step at a time.
	/// </summary>
	private async Task SaveAndProjectAsync(Core.Domains.Account.Account account)
	{
		List<IEvent> events = account.Events.ToList();

		await UnitOfWork.ExecuteInTransactionAsync(
			operation: async () => await _accountRepository.SaveAsync(account: account, ct: CancellationToken.None)
		);

		foreach (IEvent @event in events)
			await _applier.ApplyAsync(@event: @event, ct: CancellationToken.None);

		await Context.SaveChangesAsync();
		Context.ChangeTracker.Clear();
	}

	private async Task<decimal> GetProjectedBalanceAsync(Guid accountId)
		=> await Context.AccountBalances
			.Where(predicate: b => b.AccountId == accountId)
			.Select(selector: b => b.Balance)
			.FirstAsync();

	[Test]
	public async Task RebuildAsync_AfterNonTrivialExchangeRateOperations_ShouldMatchIncrementallyProjectedBalance()
	{
		Core.Domains.Account.Account account = await CreateTestAccountAsync(balance: 0m, currency: "USD");
		await SaveAndProjectAsync(account: account);

		Core.Domains.Account.Account? loaded = await _accountRepository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		loaded!.Credit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 1000m,
			exchangeRate: 0.011734m,
			description: null
		);
		await SaveAndProjectAsync(account: loaded);

		loaded.Debit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 3.5m,
			exchangeRate: 1.0837m,
			description: null
		);
		await SaveAndProjectAsync(account: loaded);

		loaded.CreditTransfer(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transferId: Guid.CreateVersion7(),
			fromAccountId: Guid.CreateVersion7(),
			amount: 250m,
			exchangeRate: 0.91273m,
			description: null
		);
		await SaveAndProjectAsync(account: loaded);

		decimal balanceBeforeRebuild = await GetProjectedBalanceAsync(accountId: account.Id);
		await Assert.That(value: balanceBeforeRebuild).IsEqualTo(expected: loaded.Balance.Amount);
		await Assert.That(value: balanceBeforeRebuild).IsEqualTo(expected: 236.12m);

		await RebuildAsync(accountId: account.Id);

		decimal balanceAfterRebuild = await GetProjectedBalanceAsync(accountId: account.Id);
		await Assert.That(value: balanceAfterRebuild).IsEqualTo(expected: balanceBeforeRebuild);

		Core.Domains.Account.Account? reloaded = await _accountRepository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		await Assert.That(value: reloaded!.Balance.Amount).IsEqualTo(expected: balanceAfterRebuild);
	}

	[Test]
	public async Task RebuildAsync_AfterManySmallConversions_ShouldNotAccumulateDrift()
	{
		Core.Domains.Account.Account account = await CreateTestAccountAsync(balance: 0m, currency: "EUR");
		await SaveAndProjectAsync(account: account);

		Core.Domains.Account.Account? loaded = await _accountRepository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		for (int i = 0; i < 50; i++)
		{
			loaded!.Credit(
				occurredAt: FakeDateProvider.Default.UtcNow,
				transactionId: Guid.CreateVersion7(),
				categoryId: Guid.CreateVersion7(),
				amount: 7m,
				exchangeRate: 0.333333m,
				description: null
			);
			await SaveAndProjectAsync(account: loaded);
		}

		decimal balanceBeforeRebuild = await GetProjectedBalanceAsync(accountId: account.Id);
		await Assert.That(value: balanceBeforeRebuild).IsEqualTo(expected: loaded!.Balance.Amount);

		await RebuildAsync(accountId: account.Id);

		decimal balanceAfterRebuild = await GetProjectedBalanceAsync(accountId: account.Id);
		await Assert.That(value: balanceAfterRebuild).IsEqualTo(expected: balanceBeforeRebuild);
	}
}
