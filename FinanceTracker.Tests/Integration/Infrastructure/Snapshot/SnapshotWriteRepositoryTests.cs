using FinanceTracker.Infrastructure.Database.Entities;
using FinanceTracker.Infrastructure.Database.Repositories.Snapshot;
using FinanceTracker.Tests.Integration.Infrastructure._Shared;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Snapshot;

public sealed class SnapshotWriteRepositoryTests : DatabaseFixture
{
	private SnapshotWriteRepository _repository = null!;

	[Before(hookType: Test)]
	public void SetupRepository()
		=> _repository = new SnapshotWriteRepository(context: Context);

	private async Task InsertSnapshotAsync(Guid aggregateId, string aggregateType, int version)
	{
		await Context.Snapshots.AddAsync(entity: new SnapshotEntity
		{
			AggregateId = aggregateId,
			AggregateType = aggregateType,
			Version = version,
			State = "{}",
			CreatedAt = DateTime.UtcNow
		});
		await Context.SaveChangesAsync();
	}

	[Test]
	public async Task DeleteOldAsync_WhenNoSnapshots_ReturnsZero()
	{
		int deleted = await _repository.DeleteOldAsync(batchSize: 100);

		await Assert.That(value: deleted).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task DeleteOldAsync_WhenOneSnapshotPerAggregate_DeletesNothing()
	{
		Guid aggregateId = Guid.CreateVersion7();
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 10);

		int deleted = await _repository.DeleteOldAsync(batchSize: 100);

		await Assert.That(value: deleted).IsEqualTo(expected: 0);
		int remaining = await Context.Snapshots.CountAsync();
		await Assert.That(value: remaining).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task DeleteOldAsync_WhenMultipleSnapshotsPerAggregate_KeepsOnlyLatest()
	{
		Guid aggregateId = Guid.CreateVersion7();
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 10);
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 20);
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 30);

		int deleted = await _repository.DeleteOldAsync(batchSize: 100);

		await Assert.That(value: deleted).IsEqualTo(expected: 2);

		SnapshotEntity surviving = await Context.Snapshots.SingleAsync();
		await Assert.That(value: surviving.Version).IsEqualTo(expected: 30);
	}

	[Test]
	public async Task DeleteOldAsync_WhenMultipleAggregates_KeepsLatestForEach()
	{
		Guid aggregateId1 = Guid.CreateVersion7();
		Guid aggregateId2 = Guid.CreateVersion7();

		await InsertSnapshotAsync(aggregateId: aggregateId1, aggregateType: "Account", version: 10);
		await InsertSnapshotAsync(aggregateId: aggregateId1, aggregateType: "Account", version: 20);
		await InsertSnapshotAsync(aggregateId: aggregateId2, aggregateType: "Account", version: 50);
		await InsertSnapshotAsync(aggregateId: aggregateId2, aggregateType: "Account", version: 100);

		int deleted = await _repository.DeleteOldAsync(batchSize: 100);

		await Assert.That(value: deleted).IsEqualTo(expected: 2);

		bool latestOfFirst = await Context.Snapshots.AnyAsync(predicate: s => s.AggregateId == aggregateId1 && s.Version == 20);
		bool latestOfSecond = await Context.Snapshots.AnyAsync(predicate: s => s.AggregateId == aggregateId2 && s.Version == 100);

		await Assert.That(value: latestOfFirst).IsTrue();
		await Assert.That(value: latestOfSecond).IsTrue();
	}

	[Test]
	public async Task DeleteOldAsync_WhenBatchSizeSmallerThanTotal_ReturnsOnlyBatchCount()
	{
		Guid aggregateId = Guid.CreateVersion7();
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 10);
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 20);
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 30);
		await InsertSnapshotAsync(aggregateId: aggregateId, aggregateType: "Account", version: 40);

		int deleted = await _repository.DeleteOldAsync(batchSize: 2);

		await Assert.That(value: deleted).IsEqualTo(expected: 2);
	}
}