using System.Text.Json;
using FinanceTracker.Contracts.Events.Account.Abstraction;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Core.Services.Correlation;
using FinanceTracker.Core.ValueObjects;
using FinanceTracker.Infrastructure.Database.Context;
using FinanceTracker.Infrastructure.Database.EventStore;
using FinanceTracker.Infrastructure.Database.EventStore.TypeResolver;
using FinanceTracker.Infrastructure.Database.Repositories.Account;
using FinanceTracker.Infrastructure.EventMapping.Integration;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Account;

public sealed class AccountRepositoryTests : DatabaseFixture
{
	private AccountRepository _repository = null!;
	private readonly AccountSnapshotSerializer _serializer = new AccountSnapshotSerializer();

	private PostgresEventStore CreateEventStore() => new PostgresEventStore(
		context: new FinanceTrackerContext(
			options: new DbContextOptionsBuilder<FinanceTrackerContext>().UseNpgsql(connectionString: Context.Database.GetConnectionString()!).Options
		),
		eventTypeResolver: new EventTypeResolver(
			assembly: typeof(FinanceTracker.Core.Domains.Abstractions.EventStore.Event.IEvent).Assembly,
			logger: Substitute.For<ILogger<EventTypeResolver>>()
		),
		integrationEventMapper: new AccountIntegrationEventMapper(logger: Substitute.For<ILogger<AccountIntegrationEventMapper>>()),
		integrationEventTypeResolver: new IntegrationEventTypeResolver(
			contractsAssembly: typeof(IAccountIntegrationEvent).Assembly,
			logger: Substitute.For<ILogger<IntegrationEventTypeResolver>>()
		),
		dateProvider: FakeDateProvider.Default,
		correlationContext: Substitute.For<ICorrelationContext>(),
		upcasterRegistry: CreatePassthroughUpcasterRegistry(),
		options: new FakeOptionsMonitor<EventStoreOptions>(value: new EventStoreOptions()),
		logger: Substitute.For<ILogger<PostgresEventStore>>()
	);

	private static IEventUpcasterRegistry CreatePassthroughUpcasterRegistry()
	{
		IEventUpcasterRegistry registry = Substitute.For<IEventUpcasterRegistry>();
		registry.HasChain(eventType: Arg.Any<string>()).Returns(returnThis: false);
		return registry;
	}

	[Before(hookType: Test)]
	public void SetupRepository()
	{
		_repository = new AccountRepository(
			eventStore: CreateEventStore(),
			snapshotSerializer: _serializer
		);
	}
	
	[Test]
	public async Task GetByIdAsync_WhenAccountDoesNotExist_ShouldReturnNull()
	{
		Core.Domains.Account.Account? result = await _repository.GetByIdAsync(
			accountId: Guid.CreateVersion7(),
			ct: CancellationToken.None
		);

		await Assert.That(value: result).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_AfterSave_ShouldReturnCorrectState()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(balance: 5000m).Value!;

		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(
			accountId: account.Id,
			ct: CancellationToken.None
		);

		await Assert.That(value: restored).IsNotNull();
		await Assert.That(value: restored!.Id).IsEqualTo(expected: account.Id);
		await Assert.That(value: restored.UserId).IsEqualTo(expected: account.UserId);
		await Assert.That(value: restored.Balance.Amount).IsEqualTo(expected: 5000m);
		await Assert.That(value: restored.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: restored.IsArchived).IsFalse();
	}

	[Test]
	public async Task GetByIdAsync_AfterDebit_ShouldReturnReducedBalance()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(balance: 10000m).Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		loaded!.Debit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 3000m,
			exchangeRate: 1m,
			description: null
		);
		await _repository.SaveAsync(account: loaded, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		await Assert.That(value: restored!.Balance.Amount).IsEqualTo(expected: 7000m);
		await Assert.That(value: restored.Version).IsEqualTo(expected: loaded.Version);
	}

	[Test]
	public async Task GetByIdAsync_AfterCredit_ShouldReturnIncreasedBalance()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(balance: 1000m).Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		loaded!.Credit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 500m,
			exchangeRate: 1m,
			description: null
		);
		await _repository.SaveAsync(account: loaded, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		await Assert.That(value: restored!.Balance.Amount).IsEqualTo(expected: 1500m);
	}

	[Test]
	public async Task GetByIdAsync_AfterArchive_ShouldReturnArchivedState()
	{
		Core.Domains.Account.Account account = AccountFactory.Create().Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		loaded!.Archive(occurredAt: FakeDateProvider.Default.UtcNow);
		await _repository.SaveAsync(account: loaded, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		await Assert.That(value: restored!.IsArchived).IsTrue();
	}

	[Test]
	public async Task GetByIdAsync_AfterRename_ShouldReturnNewName()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(name: "Старое имя").Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		Name newName = Name.Create(value: "Новое имя").Value!;
		loaded!.Rename(occurredAt: FakeDateProvider.Default.UtcNow, newName: newName);
		await _repository.SaveAsync(account: loaded, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		await Assert.That(value: restored!.Name).IsEqualTo(expected: newName);
	}

	[Test]
	public async Task GetByIdAsync_AfterMultipleOperations_ShouldReflectFinalState()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(balance: 10000m).Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		loaded!.Debit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 2000m,
			exchangeRate: 1m,
			description: null
		);
		loaded.Credit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 500m,
			exchangeRate: 1m,
			description: null
		);
		loaded.Debit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 1000m,
			exchangeRate: 1m,
			description: null
		);
		await _repository.SaveAsync(account: loaded, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		// 10000 - 2000 + 500 - 1000 = 7500
		await Assert.That(value: restored!.Balance.Amount).IsEqualTo(expected: 7500m);
	}

	[Test]
	public async Task SaveAsync_WithNoEvents_ShouldNotThrowAndNotPersistAnything()
	{
		Core.Domains.Account.Account account = AccountFactory.CreateWithArchivation();

		await Assert.That(action: async () => await _repository.SaveAsync(account: account, ct: CancellationToken.None)).ThrowsNothing();

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		await Assert.That(value: loaded).IsNull();
	}

	[Test]
	public async Task SaveAsync_ThenSaveAgain_ShouldAccumulateVersionCorrectly()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(balance: 1000m).Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);
		int versionAfterCreate = account.Version;

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		loaded!.Debit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 100m,
			exchangeRate: 1m,
			description: null
		);
		await _repository.SaveAsync(account: loaded, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		await Assert.That(value: restored!.Version).IsGreaterThan(minimum: versionAfterCreate);
	}

	[Test]
	public async Task SaveAsync_AfterClearEvents_ShouldBeIdempotent()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(balance: 1000m).Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		await Assert.That(value: restored!.Balance.Amount).IsEqualTo(expected: 1000m);
	}
	
	[Test]
	public async Task GetByIdAsync_WithExchangeRate_ShouldApplyConversionCorrectly()
	{
		Core.Domains.Account.Account account = AccountFactory.Create(balance: 0m, currency: "RUB").Value!;
		await _repository.SaveAsync(account: account, ct: CancellationToken.None);

		Core.Domains.Account.Account? loaded = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);
		loaded!.Credit(
			occurredAt: FakeDateProvider.Default.UtcNow,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 100m,
			exchangeRate: 90m,
			description: null
		);
		await _repository.SaveAsync(account: loaded, ct: CancellationToken.None);

		Core.Domains.Account.Account? restored = await _repository.GetByIdAsync(accountId: account.Id, ct: CancellationToken.None);

		await Assert.That(value: restored!.Balance.Amount).IsEqualTo(expected: 9000m);
	}
}