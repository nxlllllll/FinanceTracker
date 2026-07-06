using FinanceTracker.Infrastructure.Database.Context.Outbox;
using FinanceTracker.Infrastructure.Database.Repositories.Outbox;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using FinanceTracker.Tests.Unit.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.Outbox;

public sealed class OutboxWriteRepositoryTests : DatabaseFixture
{
	private OutboxWriteRepository _repository = null!;

	[Before(hookType: Test)]
	public void Setup()
		=> _repository = new OutboxWriteRepository(context: Context, dateProvider: FakeDateProvider.Default);

	private async Task<Guid> SeedMessageAsync(
		DateTimeOffset? lockedUntil = null,
		DateTimeOffset? updatedAt = null,
		DateTimeOffset? processedAt = null,
		int retryCount = 0,
		DateTimeOffset? failedAt = null)
	{
		Guid id = Guid.CreateVersion7();
		await Context.OutboxMessages.AddAsync(entity: new OutboxMessageEntity
		{
			Id = id,
			AggregateId = Guid.CreateVersion7(),
			AggregateType = "Account",
			Payload = "{}",
			UpdatedAt = updatedAt ?? FakeDateProvider.Default.UtcNow,
			ProcessedAt = processedAt,
			RetryCount = retryCount,
			FailedAt = failedAt,
			LockedUntil = lockedUntil
		});
		await Context.SaveChangesAsync();
		return id;
	}

	[Test]
	public async Task MarkAsPublishedAsync_ShouldSetProcessedAt()
	{
		Guid id = await SeedMessageAsync();
		DateTimeOffset processedAt = FakeDateProvider.Default.UtcNow.AddMinutes(minutes: 1);

		await _repository.MarkAsPublishedAsync(messageId: id, processedAt: processedAt, ct: CancellationToken.None);

		OutboxMessageEntity entity = await Context.OutboxMessages.AsNoTracking().FirstAsync(predicate: m => m.Id == id);
		await Assert.That(value: entity.ProcessedAt).IsEqualTo(expected: processedAt);
	}

	[Test]
	public async Task MarkAsPublishedAsync_WithUnknownMessageId_ShouldNotThrow()
	{
		await Assert.That(action: async () => await _repository.MarkAsPublishedAsync(
			messageId: Guid.CreateVersion7(),
			processedAt: FakeDateProvider.Default.UtcNow,
			ct: CancellationToken.None
		)).ThrowsNothing();
	}

	[Test]
	public async Task MarkAsFailedAsync_ShouldIncrementRetryCount()
	{
		Guid id = await SeedMessageAsync(retryCount: 2);

		await _repository.MarkAsFailedAsync(messageId: id, retryCount: 3, failedAt: null, ct: CancellationToken.None);

		OutboxMessageEntity entity = await Context.OutboxMessages.AsNoTracking().FirstAsync(predicate: m => m.Id == id);
		await Assert.That(value: entity.RetryCount).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task MarkAsFailedAsync_WithFailedAt_ShouldSetFailedAt()
	{
		Guid id = await SeedMessageAsync();
		DateTimeOffset failedAt = FakeDateProvider.Default.UtcNow;

		await _repository.MarkAsFailedAsync(messageId: id, retryCount: 1, failedAt: failedAt, ct: CancellationToken.None);

		OutboxMessageEntity entity = await Context.OutboxMessages.AsNoTracking().FirstAsync(predicate: m => m.Id == id);
		await Assert.That(value: entity.FailedAt).IsEqualTo(expected: failedAt);
	}

	[Test]
	public async Task MarkAsFailedAsync_WithNullFailedAt_ShouldClearPreviousFailedAt()
	{
		// Simulates a retry attempt that hasn't exhausted the retry budget yet: the message
		// was previously marked failed, but this call represents another in-flight attempt,
		// so FailedAt must be cleared rather than left stale from the prior failure.
		Guid id = await SeedMessageAsync(failedAt: FakeDateProvider.Default.UtcNow.AddMinutes(minutes: -5));

		await _repository.MarkAsFailedAsync(messageId: id, retryCount: 1, failedAt: null, ct: CancellationToken.None);

		OutboxMessageEntity entity = await Context.OutboxMessages.AsNoTracking().FirstAsync(predicate: m => m.Id == id);
		await Assert.That(value: entity.FailedAt).IsNull();
	}

	[Test]
	public async Task MarkAsFailedAsync_ShouldReleaseLock()
	{
		// The escalation path (MarkAsFailedAsync) always releases the lease, even though the
		// message won't be retried further after final failure — a stale LockedUntil would
		// otherwise make the message invisible to ClaimPendingBatchAsync for no reason.
		Guid id = await SeedMessageAsync(lockedUntil: FakeDateProvider.Default.UtcNow.AddSeconds(seconds: 60));

		await _repository.MarkAsFailedAsync(messageId: id, retryCount: 1, failedAt: FakeDateProvider.Default.UtcNow, ct: CancellationToken.None);

		OutboxMessageEntity entity = await Context.OutboxMessages.AsNoTracking().FirstAsync(predicate: m => m.Id == id);
		await Assert.That(value: entity.LockedUntil).IsNull();
	}

	[Test]
	public async Task MarkAsFailedAsync_ShouldUpdateUpdatedAtToCurrentTime()
	{
		Guid id = await SeedMessageAsync(updatedAt: FakeDateProvider.Default.UtcNow.AddDays(days: -1));

		await _repository.MarkAsFailedAsync(messageId: id, retryCount: 1, failedAt: null, ct: CancellationToken.None);

		OutboxMessageEntity entity = await Context.OutboxMessages.AsNoTracking().FirstAsync(predicate: m => m.Id == id);
		await Assert.That(value: entity.UpdatedAt).IsEqualTo(expected: FakeDateProvider.Default.UtcNow);
	}

	[Test]
	public async Task MarkAsFailedAsync_WithUnknownMessageId_ShouldNotThrow()
	{
		await Assert.That(action: async () => await _repository.MarkAsFailedAsync(
			messageId: Guid.CreateVersion7(),
			retryCount: 1,
			failedAt: FakeDateProvider.Default.UtcNow,
			ct: CancellationToken.None
		)).ThrowsNothing();
	}

	[Test]
	public async Task DeleteProcessedAsync_ShouldDeleteOnlyMessagesProcessedBeforeCutoff()
	{
		DateTimeOffset cutoff = FakeDateProvider.Default.UtcNow;
		Guid oldProcessed = await SeedMessageAsync(processedAt: cutoff.AddDays(days: -2));
		Guid recentProcessed = await SeedMessageAsync(processedAt: cutoff.AddDays(days: 2));
		Guid unprocessed = await SeedMessageAsync(processedAt: null);

		int deletedCount = await _repository.DeleteProcessedAsync(before: cutoff, batchSize: 100, ct: CancellationToken.None);

		await Assert.That(value: deletedCount).IsEqualTo(expected: 1);
		await Assert.That(value: await Context.OutboxMessages.AnyAsync(predicate: m => m.Id == oldProcessed)).IsFalse();
		await Assert.That(value: await Context.OutboxMessages.AnyAsync(predicate: m => m.Id == recentProcessed)).IsTrue();
		await Assert.That(value: await Context.OutboxMessages.AnyAsync(predicate: m => m.Id == unprocessed)).IsTrue();
	}

	[Test]
	public async Task DeleteProcessedAsync_ShouldRespectBatchSize()
	{
		DateTimeOffset cutoff = FakeDateProvider.Default.UtcNow;
		await SeedMessageAsync(processedAt: cutoff.AddDays(days: -3));
		await SeedMessageAsync(processedAt: cutoff.AddDays(days: -2));
		await SeedMessageAsync(processedAt: cutoff.AddDays(days: -1));

		int deletedCount = await _repository.DeleteProcessedAsync(before: cutoff, batchSize: 2, ct: CancellationToken.None);

		await Assert.That(value: deletedCount).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task DeleteFailedAsync_ShouldDeleteOnlyMessagesFailedBeforeCutoff()
	{
		DateTimeOffset cutoff = FakeDateProvider.Default.UtcNow;
		Guid oldFailed = await SeedMessageAsync(failedAt: cutoff.AddDays(days: -2));
		Guid recentFailed = await SeedMessageAsync(failedAt: cutoff.AddDays(days: 2));
		Guid notFailed = await SeedMessageAsync(failedAt: null);

		int deletedCount = await _repository.DeleteFailedAsync(before: cutoff, batchSize: 100, ct: CancellationToken.None);

		await Assert.That(value: deletedCount).IsEqualTo(expected: 1);
		await Assert.That(value: await Context.OutboxMessages.AnyAsync(predicate: m => m.Id == oldFailed)).IsFalse();
		await Assert.That(value: await Context.OutboxMessages.AnyAsync(predicate: m => m.Id == recentFailed)).IsTrue();
		await Assert.That(value: await Context.OutboxMessages.AnyAsync(predicate: m => m.Id == notFailed)).IsTrue();
	}
}
