using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Results;
using FinanceTracker.Infrastructure.Database.Context.UnresolvableEvent;
using FinanceTracker.Infrastructure.Database.Repositories.UnresolvableEvent;
using FinanceTracker.Tests.Integration._Shared.Fixtures;
using EventReadModel = FinanceTracker.Core.ReadModels.UnresolvableEvent.UnresolvableEvent;

namespace FinanceTracker.Tests.Integration.Infrastructure.Repositories.UnresolvableEvent;

public sealed class UnresolvableEventReadRepositoryTests : DatabaseFixture
{
	private UnresolvableEventReadRepository Repository => new UnresolvableEventReadRepository(context: Context);

	private static readonly DateTimeOffset Origin = new DateTimeOffset(
		year: 2026, month: 1, day: 1, hour: 0, minute: 0, second: 0, offset: TimeSpan.Zero
	);

	private async Task<Guid> AddAsync(
		int minutesFromOrigin,
		DateTimeOffset? acknowledgedAt = null,
		DateTimeOffset? resolvedAt = null)
	{
		Guid id = Guid.CreateVersion7();

		await Context.UnresolvableEvents.AddAsync(entity: new UnresolvableEventEntity
		{
			Id = id,
			Type = UnresolvableEventType.TransferCompensation,
			ReferenceId = Guid.CreateVersion7(),
			Reason = "refund refused",
			Payload = "{}",
			OccurredAt = Origin.AddMinutes(minutes: minutesFromOrigin),
			AcknowledgedAt = acknowledgedAt,
			ResolvedAt = resolvedAt
		});

		await Context.SaveChangesAsync();

		return id;
	}

	[Test]
	public async Task GetUnacknowledgedBatchAsync_ShouldSkipWhatHasAlreadyBeenReportedOrFixed()
	{
		Guid pending = await AddAsync(minutesFromOrigin: 0);
		await AddAsync(minutesFromOrigin: 1, acknowledgedAt: Origin);
		await AddAsync(minutesFromOrigin: 2, resolvedAt: Origin);

		PagedResult<EventReadModel> result = await Repository.GetUnacknowledgedBatchAsync(batchSize: 10);

		await Assert.That(value: result.Items.Select(selector: item => item.Id)).IsEquivalentTo(expected: new[] { pending })
			.Because(message: "reporting an entry that was already reported would drown the channel it is reported to");
	}

	[Test]
	public async Task GetUnacknowledgedBatchAsync_ShouldReturnOldestFirst()
	{
		Guid third = await AddAsync(minutesFromOrigin: 30);
		Guid first = await AddAsync(minutesFromOrigin: 0);
		Guid second = await AddAsync(minutesFromOrigin: 10);

		PagedResult<EventReadModel> result = await Repository.GetUnacknowledgedBatchAsync(batchSize: 10);

		await Assert.That(value: result.Items.Select(selector: item => item.Id).ToList())
			.IsEquivalentTo(expected: new[] { first, second, third });
	}

	[Test]
	public async Task GetUnacknowledgedBatchAsync_WhenMoreRemain_ShouldReportAnotherPageWithoutLeakingTheProbeRow()
	{
		for (int minute = 0; minute < 5; minute++)
			await AddAsync(minutesFromOrigin: minute);

		PagedResult<EventReadModel> result = await Repository.GetUnacknowledgedBatchAsync(batchSize: 2);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.HasNextPage).IsTrue();
		await Assert.That(value: result.NextCursorId).IsEqualTo(expected: result.Items[^1].Id);
		await Assert.That(value: result.NextCursorDate).IsEqualTo(expected: result.Items[^1].OccurredAt);
	}

	[Test]
	public async Task GetUnacknowledgedBatchAsync_WhenExactlyOneBatchRemains_ShouldNotClaimAnotherPage()
	{
		for (int minute = 0; minute < 2; minute++)
			await AddAsync(minutesFromOrigin: minute);

		PagedResult<EventReadModel> result = await Repository.GetUnacknowledgedBatchAsync(batchSize: 2);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.HasNextPage).IsFalse()
			.Because(message: "a full page is not evidence of a further one, and claiming otherwise costs an empty round trip every cycle");

		await Assert.That(value: result.NextCursorId).IsNull();
		await Assert.That(value: result.NextCursorDate).IsNull();
	}

	[Test]
	public async Task GetUnacknowledgedBatchAsync_WithNothingPending_ShouldReturnAnEmptyPage()
	{
		await AddAsync(minutesFromOrigin: 0, acknowledgedAt: Origin);

		PagedResult<EventReadModel> result = await Repository.GetUnacknowledgedBatchAsync(batchSize: 10);

		await Assert.That(value: result.Items).IsEmpty();
		await Assert.That(value: result.HasNextPage).IsFalse();
		await Assert.That(value: result.NextCursorId).IsNull();
	}

	[Test]
	public async Task GetUnresolvedOlderThanAsync_ShouldStillCountWhatWasAcknowledgedButNeverFixed()
	{
		await AddAsync(minutesFromOrigin: 0, acknowledgedAt: Origin);
		await AddAsync(minutesFromOrigin: 1);
		await AddAsync(minutesFromOrigin: 2, resolvedAt: Origin);

		UnresolvedBacklogSummary summary = await Repository.GetUnresolvedOlderThanAsync(
			cutoff: Origin.AddHours(hours: 1),
			sampleSize: 10
		);

		await Assert.That(value: summary.TotalCount).IsEqualTo(expected: 2)
			.Because(message: "acknowledging an entry records that someone looked at it, not that the money moved");
	}

	[Test]
	public async Task GetUnresolvedOlderThanAsync_ShouldIgnoreAnythingNewerThanTheCutoff()
	{
		await AddAsync(minutesFromOrigin: 0);
		await AddAsync(minutesFromOrigin: 120);

		UnresolvedBacklogSummary summary = await Repository.GetUnresolvedOlderThanAsync(
			cutoff: Origin.AddHours(hours: 1),
			sampleSize: 10
		);

		await Assert.That(value: summary.TotalCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task GetUnresolvedOlderThanAsync_ShouldCapTheSampleWithoutCappingTheCount()
	{
		for (int minute = 0; minute < 6; minute++)
			await AddAsync(minutesFromOrigin: minute);

		UnresolvedBacklogSummary summary = await Repository.GetUnresolvedOlderThanAsync(
			cutoff: Origin.AddHours(hours: 1),
			sampleSize: 2
		);

		await Assert.That(value: summary.TotalCount).IsEqualTo(expected: 6)
			.Because(message: "the count is the alert threshold; capping it would hide the size of the backlog behind the sample size");

		await Assert.That(value: summary.Sample.Count).IsEqualTo(expected: 2);
		await Assert.That(value: summary.OldestOccurredAt).IsEqualTo(expected: Origin);
	}

	[Test]
	public async Task GetUnresolvedOlderThanAsync_WithNothingOutstanding_ShouldReportAnEmptyBacklog()
	{
		await AddAsync(minutesFromOrigin: 0, resolvedAt: Origin);

		UnresolvedBacklogSummary summary = await Repository.GetUnresolvedOlderThanAsync(
			cutoff: Origin.AddHours(hours: 1),
			sampleSize: 10
		);

		await Assert.That(value: summary.TotalCount).IsEqualTo(expected: 0);
		await Assert.That(value: summary.OldestOccurredAt).IsNull();
		await Assert.That(value: summary.Sample).IsEmpty();
	}

	[Test]
	public async Task CountUnresolvedAsync_ShouldCountEverythingStillAwaitingAHuman()
	{
		await AddAsync(minutesFromOrigin: 0);
		await AddAsync(minutesFromOrigin: 1, acknowledgedAt: Origin);
		await AddAsync(minutesFromOrigin: 2, resolvedAt: Origin);
		await AddAsync(minutesFromOrigin: 3, acknowledgedAt: Origin, resolvedAt: Origin);

		await Assert.That(value: await Repository.CountUnresolvedAsync()).IsEqualTo(expected: 2)
			.Because(message: "this feeds the gauge an alert fires on, so it has to drop only when the work is actually done");
	}
}
