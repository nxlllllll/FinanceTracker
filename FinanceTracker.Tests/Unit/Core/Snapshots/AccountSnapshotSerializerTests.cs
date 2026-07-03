using FinanceTracker.Core.Domains.Abstractions.Aggregate;
using FinanceTracker.Core.Domains.Abstractions.Snapshot;
using FinanceTracker.Core.Domains.Account;
using FinanceTracker.Tests.Unit.Helpers;

namespace FinanceTracker.Tests.Unit.Core.Snapshots;

public sealed class AccountSnapshotSerializerTests
{
	private static DateTimeOffset Now => FakeDateProvider.Default.UtcNow;
	private readonly ISnapshotSerializer<Account> _serializer = new AccountSnapshotSerializer();

	[Test]
	public async Task Serialize_ShouldProduceNonEmptyJson()
	{
		Account account = AccountFactory.Create(balance: 5000m).Value!;

		string json = _serializer.Serialize(aggregate: account);

		await Assert.That(value: json).IsNotEmpty();
	}

	[Test]
	public async Task Deserialize_ShouldRestoreAllFields()
	{
		Guid userId = Guid.CreateVersion7();
		Account original = AccountFactory.Create(userId: userId, balance: 7500m).Value!;
		original.Archive(occurredAt: Now);

		string json = _serializer.Serialize(aggregate: original);
		SnapshotData snapshot = new SnapshotData(
			AggregateId: original.Id,
			AggregateType: AggregateTypeNames.Account,
			Version: original.Version,
			State: json
		);

		Account restored = _serializer.Deserialize(snapshot: snapshot);

		await Assert.That(value: restored.Id).IsEqualTo(expected: original.Id);
		await Assert.That(value: restored.UserId).IsEqualTo(expected: userId);
		await Assert.That(value: restored.Name).IsEqualTo(expected: original.Name);
		await Assert.That(value: restored.Type).IsEqualTo(expected: original.Type);
		await Assert.That(value: restored.Balance.Amount).IsEqualTo(expected: 7500m);
		await Assert.That(value: restored.Currency.Value).IsEqualTo(expected: "RUB");
		await Assert.That(value: restored.IsArchived).IsTrue();
		await Assert.That(value: restored.Version).IsEqualTo(expected: original.Version);
	}

	[Test]
	public async Task Serialize_Deserialize_IsIdempotent()
	{
		Account original = AccountFactory.Create(balance: 1000m).Value!;

		string json1 = _serializer.Serialize(aggregate: original);
		SnapshotData snapshot = new SnapshotData(
			AggregateId: original.Id,
			AggregateType: AggregateTypeNames.Account,
			Version: original.Version,
			State: json1
		);
		Account restored = _serializer.Deserialize(snapshot: snapshot);
		string json2 = _serializer.Serialize(aggregate: restored);

		await Assert.That(value: json1).IsEqualTo(expected: json2);
	}

	[Test]
	public async Task Deserialize_AfterMultipleEvents_ShouldPreserveLatestState()
	{
		Account account = AccountFactory.Create(balance: 10000m).Value!;

		account.Debit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 3000m,
			exchangeRate: 1m,
			description: null
		);

		account.Credit(
			occurredAt: Now,
			transactionId: Guid.CreateVersion7(),
			categoryId: Guid.CreateVersion7(),
			amount: 500m,
			exchangeRate: 1m,
			description: null
		);

		string json = _serializer.Serialize(aggregate: account);
		SnapshotData snapshot = new SnapshotData(
			AggregateId: account.Id,
			AggregateType: AggregateTypeNames.Account,
			Version: account.Version,
			State: json
		);

		Account restored = _serializer.Deserialize(snapshot: snapshot);

		await Assert.That(value: restored.Balance.Amount).IsEqualTo(expected: 7500m);
		await Assert.That(value: restored.Version).IsEqualTo(expected: account.Version);
	}
}
