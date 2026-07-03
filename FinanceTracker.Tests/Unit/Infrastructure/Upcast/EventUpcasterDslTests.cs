using FinanceTracker.Core.Domains.Abstractions.EventStore.Upcast;

namespace FinanceTracker.Tests.Unit.Infrastructure.Upcast;

public sealed class UpcasterWithoutVersionAttribute : EventUpcaster<AccountCreatedV1, AccountCreatedV2>
{
	public override AccountCreatedV2 Upcast(AccountCreatedV1 source)
	{
		return new AccountCreatedV2(
			Id: source.Id,
			AccountId: source.AccountId,
			UserId: source.UserId,
			Name: source.Name,
			Currency: source.Currency,
			IsArchived: false,
			Version: source.Version,
			OccurredAt: source.OccurredAt
		);
	}
}

public sealed record NoEventTypeRecord(Guid Id);

[UpcasterVersion(from: 1, to: 2)]
public sealed class UpcasterWithoutEventTypeOnTFrom : EventUpcaster<NoEventTypeRecord, AccountCreatedV2>
{
	public override AccountCreatedV2 Upcast(NoEventTypeRecord source)
	{
		return new AccountCreatedV2(
			Id: source.Id,
			AccountId: Guid.Empty,
			UserId: Guid.Empty,
			Name: String.Empty,
			Currency: String.Empty,
			IsArchived: false,
			Version: 1,
			OccurredAt: DateTimeOffset.UtcNow
		);
	}
}

public sealed class EventUpcasterDslTests
{
	private static readonly AccountCreatedV1 SampleV1 = new AccountCreatedV1(
		Id: Guid.CreateVersion7(),
		AccountId: Guid.CreateVersion7(),
		UserId: Guid.CreateVersion7(),
		Name: "Test",
		Currency: "RUB",
		Version: 1,
		OccurredAt: DateTimeOffset.UtcNow
	);

	[Test]
	public async Task Upcaster_ShouldMapAllFieldsCorrectly()
	{
		AccountCreatedV1ToV2Upcaster upcaster = new AccountCreatedV1ToV2Upcaster();

		AccountCreatedV2 v2 = upcaster.Upcast(source: SampleV1);

		await Assert.That(value: v2.Id).IsEqualTo(expected: SampleV1.Id);
		await Assert.That(value: v2.AccountId).IsEqualTo(expected: SampleV1.AccountId);
		await Assert.That(value: v2.UserId).IsEqualTo(expected: SampleV1.UserId);
		await Assert.That(value: v2.Name).IsEqualTo(expected: SampleV1.Name);
		await Assert.That(value: v2.Currency).IsEqualTo(expected: SampleV1.Currency);
		await Assert.That(value: v2.IsArchived).IsFalse();
	}

	[Test]
	public async Task Upcaster_ShouldReadEventTypeFromTFromAttribute()
	{
		AccountCreatedV1ToV2Upcaster upcaster = new AccountCreatedV1ToV2Upcaster();

		await Assert.That(value: upcaster.EventType).IsEqualTo(expected: "account.created.test");
	}

	[Test]
	public async Task Upcaster_ShouldReadVersionsFromUpcasterVersionAttribute()
	{
		AccountCreatedV1ToV2Upcaster upcaster = new AccountCreatedV1ToV2Upcaster();

		await Assert.That(value: upcaster.FromVersion).IsEqualTo(expected: 1);
		await Assert.That(value: upcaster.ToVersion).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task Upcaster_WhenMissingUpcasterVersionAttribute_ShouldThrowOnConstruction()
		=> await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: new UpcasterWithoutVersionAttribute()));

	[Test]
	public async Task Upcaster_WhenTFromMissingEventTypeAttribute_ShouldThrowOnConstruction()
		=> await Assert.ThrowsAsync<InvalidOperationException>(action: async () => await Task.FromResult(result: new UpcasterWithoutEventTypeOnTFrom()));

	[Test]
	public async Task Upcaster_ExplicitInterfaceUpcast_ShouldCastAndDelegate()
	{
		IEventUpcaster upcaster = new AccountCreatedV1ToV2Upcaster();

		object result = upcaster.Upcast(source: SampleV1);

		await Assert.That(value: result).IsTypeOf<AccountCreatedV2>();
	}
}
