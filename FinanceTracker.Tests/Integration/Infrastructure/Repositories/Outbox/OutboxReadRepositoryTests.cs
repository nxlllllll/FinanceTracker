using FinanceTracker.Core.Repositories.Outbox;
using FinanceTracker.Infrastructure.Database.Context.Outbox;
using FinanceTracker.Infrastructure.Database.Repositories.Outbox;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Outbox;

public sealed class OutboxReadRepositoryTests : DatabaseFixture
{
	private OutboxReadRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
		=> _repository = new OutboxReadRepository(context: Context);

	private async Task<Guid> SeedMessageAsync(
		DateTimeOffset? lockedUntil = null,
		DateTimeOffset? updatedAt = null,
		DateTimeOffset? processedAt = null,
		DateTimeOffset? failedAt = null,
		Guid? aggregateId = null)
	{
		Guid id = Guid.CreateVersion7();
		await Context.OutboxMessages.AddAsync(entity: new OutboxMessageEntity
		{
			Id = id,
			AggregateId = aggregateId ?? Guid.CreateVersion7(),
			AggregateType = "Account",
			Payload = "{}",
			UpdatedAt = updatedAt ?? FakeDateProvider.Default.UtcNow,
			ProcessedAt = processedAt,
			FailedAt = failedAt,
			LockedUntil = lockedUntil
		});
		await Context.SaveChangesAsync();
		return id;
	}

	[Test]
	public async Task ClaimPendingBatchAsync_ShouldReturnUnclaimedMessages()
	{
		Guid id = await SeedMessageAsync();

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: FakeDateProvider.Default.UtcNow,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == id)).IsTrue();
	}

	[Test]
	public async Task ClaimPendingBatchAsync_ShouldSetLockedUntil()
	{
		Guid id = await SeedMessageAsync();
		DateTimeOffset now = FakeDateProvider.Default.UtcNow;

		await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: now,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		OutboxMessageEntity entity = await Context.OutboxMessages.AsNoTracking().FirstAsync(predicate: m => m.Id == id);

		await Assert.That(value: entity.LockedUntil).IsEqualTo(expected: now.AddSeconds(seconds: 60));
	}

	[Test]
	public async Task ClaimPendingBatchAsync_WhenAlreadyClaimedAndLeaseNotExpired_ShouldNotReturnIt()
	{
		DateTimeOffset now = FakeDateProvider.Default.UtcNow;
		Guid id = await SeedMessageAsync(lockedUntil: now.AddSeconds(seconds: 30));

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: now,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == id)).IsFalse();
	}

	[Test]
	public async Task ClaimPendingBatchAsync_WhenLeaseExpired_ShouldReturnItAgain()
	{
		DateTimeOffset now = FakeDateProvider.Default.UtcNow;
		Guid id = await SeedMessageAsync(lockedUntil: now.AddSeconds(seconds: -5));

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: now,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == id)).IsTrue();
	}

	[Test]
	public async Task ClaimPendingBatchAsync_ShouldNotReturnProcessedMessages()
	{
		Guid id = await SeedMessageAsync(processedAt: FakeDateProvider.Default.UtcNow);

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: FakeDateProvider.Default.UtcNow,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == id)).IsFalse();
	}

	[Test]
	public async Task ClaimPendingBatchAsync_ShouldRespectBatchSize()
	{
		for (int i = 0; i < 5; i++)
			await SeedMessageAsync();

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 3,
			now: FakeDateProvider.Default.UtcNow,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Count).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task ClaimPendingBatchAsync_ShouldOrderByUpdatedAtAscending()
	{
		DateTimeOffset baseTime = FakeDateProvider.Default.UtcNow;
		Guid older = await SeedMessageAsync(updatedAt: baseTime.AddMinutes(minutes: -10));
		Guid newer = await SeedMessageAsync(updatedAt: baseTime);

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: baseTime,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result[0].Id).IsEqualTo(expected: older);
		await Assert.That(value: result[1].Id).IsEqualTo(expected: newer);
	}

	[Test]
	public async Task ClaimPendingBatchAsync_WhenOlderUnprocessedMessageExistsForSameAggregate_ShouldNotReturnNewer()
	{
		DateTimeOffset baseTime = FakeDateProvider.Default.UtcNow;
		Guid aggregateId = Guid.CreateVersion7();

		Guid olderMessageId = await SeedMessageAsync(updatedAt: baseTime.AddMinutes(minutes: -5), aggregateId: aggregateId);
		Guid newerMessageId = await SeedMessageAsync(updatedAt: baseTime, aggregateId: aggregateId);

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: baseTime,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == olderMessageId)).IsTrue();
		await Assert.That(value: result.Any(predicate: m => m.Id == newerMessageId)).IsFalse();
	}

	[Test]
	public async Task ClaimPendingBatchAsync_WhenOlderMessageForSameAggregateIsAlreadyProcessed_ShouldReturnNewer()
	{
		DateTimeOffset baseTime = FakeDateProvider.Default.UtcNow;
		Guid aggregateId = Guid.CreateVersion7();

		await SeedMessageAsync(updatedAt: baseTime.AddMinutes(minutes: -5), processedAt: baseTime.AddMinutes(minutes: -1), aggregateId: aggregateId);
		Guid newerMessageId = await SeedMessageAsync(updatedAt: baseTime, aggregateId: aggregateId);

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: baseTime,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == newerMessageId)).IsTrue();
	}

	[Test]
	public async Task ClaimPendingBatchAsync_WhenOlderMessageForSameAggregateHasPermanentlyFailed_ShouldReturnNewer()
	{
		DateTimeOffset baseTime = FakeDateProvider.Default.UtcNow;
		Guid aggregateId = Guid.CreateVersion7();

		await SeedMessageAsync(updatedAt: baseTime.AddMinutes(minutes: -5), failedAt: baseTime.AddMinutes(minutes: -1), aggregateId: aggregateId);
		Guid newerMessageId = await SeedMessageAsync(updatedAt: baseTime, aggregateId: aggregateId);

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: baseTime,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == newerMessageId)).IsTrue();
	}

	[Test]
	public async Task ClaimPendingBatchAsync_WhenBlockedMessageExistsForOtherAggregate_ShouldStillReturnCurrentAggregateMessage()
	{
		DateTimeOffset baseTime = FakeDateProvider.Default.UtcNow;

		await SeedMessageAsync(updatedAt: baseTime.AddMinutes(minutes: -10));
		Guid id = await SeedMessageAsync(updatedAt: baseTime);

		IReadOnlyList<PendingOutboxMessage> result = await _repository.ClaimPendingBatchAsync(
			batchSize: 10,
			now: baseTime,
			leaseDuration: TimeSpan.FromSeconds(value: 60),
			ct: CancellationToken.None
		);

		await Assert.That(value: result.Any(predicate: m => m.Id == id)).IsTrue();
	}
}
