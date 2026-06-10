using System.Text.Json;
using FinanceTracker.Core.Converters.Json;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Event;
using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;
using FinanceTracker.Infrastructure.Database.EventStore;

namespace FinanceTracker.Tests.Unit.Infrastructure.Upcast;

[EventType(name: "account.created.test")]
public sealed record AccountCreatedV3(
	Guid Id,
	Guid AccountId,
	Guid UserId,
	string Name,
	string Currency,
	bool IsArchived,
	int Priority,
	int Version,
	DateTimeOffset OccurredAt
) : IEvent
{
	public IEvent WithVersion(int version) => this with { Version = version };
}

[UpcasterVersion(from: 2, to: 3)]
public sealed class AccountCreatedV2ToV3Upcaster : EventUpcaster<AccountCreatedV2, AccountCreatedV3>
{
	public override AccountCreatedV3 Upcast(AccountCreatedV2 source) => new AccountCreatedV3(
		Id: source.Id,
		AccountId: source.AccountId,
		UserId: source.UserId,
		Name: source.Name,
		Currency: source.Currency,
		IsArchived: source.IsArchived,
		Priority: 0,
		Version: source.Version,
		OccurredAt: source.OccurredAt
	);
}

public sealed class EventUpcasterRegistryTests
{
	private static EventUpcasterRegistry CreateRegistry(params IEventUpcaster[] upcasters) 
		=> new EventUpcasterRegistry(upcasters: upcasters);

	private static string Serialize<T>(T value) 
		=> JsonSerializer.Serialize(value: value, options: FinanceTrackerJsonOptions.Payload);

	private static readonly AccountCreatedV1 SampleV1 = new AccountCreatedV1(
		Id: Guid.CreateVersion7(),
		AccountId: Guid.CreateVersion7(),
		UserId: Guid.CreateVersion7(),
		Name: "Test",
		Currency: "RUB",
		Version: 1,
		OccurredAt: DateTimeOffset.UtcNow
	);

	private static readonly AccountCreatedV2 SampleV2 = new AccountCreatedV2(
		Id: Guid.CreateVersion7(),
		AccountId: Guid.CreateVersion7(),
		UserId: Guid.CreateVersion7(),
		Name: "Test",
		Currency: "RUB",
		IsArchived: false,
		Version: 2,
		OccurredAt: DateTimeOffset.UtcNow
	);

	[Test]
	public async Task HasChain_WhenNoUpcasters_ShouldReturnFalse()
	{
		EventUpcasterRegistry registry = CreateRegistry();

		await Assert.That(value: registry.HasChain(eventType: "account.created.test")).IsFalse();
	}

	[Test]
	public async Task HasChain_WhenUpcasterRegistered_ShouldReturnTrue()
	{
		EventUpcasterRegistry registry = CreateRegistry(new AccountCreatedV1ToV2Upcaster());

		await Assert.That(value: registry.HasChain(eventType: "account.created.test")).IsTrue();
	}

	[Test]
	public async Task Apply_WhenOneUpcasterNeeded_ShouldReturnTypedEvent()
	{
		EventUpcasterRegistry registry = CreateRegistry(new AccountCreatedV1ToV2Upcaster());

		IEvent result = registry.Apply(
			eventType: "account.created.test",
			payload: Serialize(value: SampleV1),
			storedVersion: 1,
			currentVersion: 2
		);

		await Assert.That(value: result).IsTypeOf<AccountCreatedV2>();
		await Assert.That(value: ((AccountCreatedV2)result).IsArchived).IsFalse();
		await Assert.That(value: ((AccountCreatedV2)result).Name).IsEqualTo(expected: "Test");
	}

	[Test]
	public async Task Apply_WhenChainedUpcastersNeeded_ShouldApplyAllInOrder()
	{
		EventUpcasterRegistry registry = CreateRegistry(
			new AccountCreatedV1ToV2Upcaster(),
			new AccountCreatedV2ToV3Upcaster()
		);

		IEvent result = registry.Apply(
			eventType: "account.created.test",
			payload: Serialize(value: SampleV1),
			storedVersion: 1,
			currentVersion: 3
		);

		await Assert.That(value: result).IsTypeOf<AccountCreatedV3>();
		await Assert.That(value: ((AccountCreatedV3)result).Priority).IsEqualTo(expected: 0);
		await Assert.That(value: ((AccountCreatedV3)result).Name).IsEqualTo(expected: "Test");
	}

	[Test]
	public async Task Apply_WhenPartialChainNeeded_ShouldSkipAlreadyAppliedUpcasters()
	{
		EventUpcasterRegistry registry = CreateRegistry(
			new AccountCreatedV1ToV2Upcaster(),
			new AccountCreatedV2ToV3Upcaster()
		);

		IEvent result = registry.Apply(
			eventType: "account.created.test",
			payload: Serialize(value: SampleV2),
			storedVersion: 2,
			currentVersion: 3
		);

		await Assert.That(value: result).IsTypeOf<AccountCreatedV3>();
		await Assert.That(value: ((AccountCreatedV3)result).Priority).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Apply_WhenNoChainForEventType_ShouldThrow()
	{
		EventUpcasterRegistry registry = CreateRegistry();

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: registry.Apply(
			eventType: "account.created.test",
			payload: Serialize(value: SampleV1),
			storedVersion: 1,
			currentVersion: 2
		)));
	}

	[Test]
	public async Task Apply_WhenNoUpcasterForStoredVersion_ShouldThrow()
	{
		EventUpcasterRegistry registry = CreateRegistry(new AccountCreatedV1ToV2Upcaster());

		await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: registry.Apply(
			eventType: "account.created.test",
			payload: Serialize(value: SampleV2),
			storedVersion: 5,
			currentVersion: 6
		)));
	}
}