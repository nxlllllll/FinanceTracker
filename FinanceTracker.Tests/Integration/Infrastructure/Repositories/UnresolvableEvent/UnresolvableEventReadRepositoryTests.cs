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
		DateTimeOffset? resolvedAt = null,
		UnresolvableEventType type = UnresolvableEventType.TransferCompensation,
		string payload = "{}")
	{
		Guid id = Guid.CreateVersion7();

		await Context.UnresolvableEvents.AddAsync(entity: new UnresolvableEventEntity
		{
			Id = id,
			Type = type,
			ReferenceId = Guid.CreateVersion7(),
			Reason = "refund refused",
			Payload = payload,
			OccurredAt = Origin.AddMinutes(minutes: minutesFromOrigin),
			AcknowledgedAt = acknowledgedAt,
			ResolvedAt = resolvedAt
		});

		await Context.SaveChangesAsync();

		return id;
	}

	[Test]
	public async Task GetByIdAsync_WithNoSuchEvent_ShouldReturnNull()
	{
		await Assert.That(value: await Repository.GetByIdAsync(eventId: Guid.CreateVersion7())).IsNull();
	}

	[Test]
	public async Task GetByIdAsync_ShouldCarryThePayloadTheListLeavesOut()
	{
		Guid id = await AddAsync(minutesFromOrigin: 0, payload: """{"amount":100}""");

		UnresolvableEventDetail? detail = await Repository.GetByIdAsync(eventId: id);

		await Assert.That(value: detail).IsNotNull();
		await Assert.That(value: detail!.Payload).IsEqualTo(expected: """{"amount": 100}""").Because(message: """
			The payload is the only reason to open a single event: it holds what was being applied when the
			operation was refused. The column is jsonb, so what comes back is Postgres's normalisation of
			what went in rather than the exact bytes — the body itself has to survive, its spacing need not.
		""");
	}

	[Test]
	public async Task GetAllAsync_ShouldReturnOldestFirst()
	{
		Guid third = await AddAsync(minutesFromOrigin: 30);
		Guid first = await AddAsync(minutesFromOrigin: 0);
		Guid second = await AddAsync(minutesFromOrigin: 10);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync();

		await Assert.That(value: result.Items.Select(selector: item => item.Id).ToList())
			.IsEquivalentTo(expected: new[] { first, second, third }).Because(message: """
				This is a work queue, not a feed. Newest first would bury the entry that has been waiting
				longest under everything that broke since.
			""");
	}

	[Test]
	public async Task GetAllAsync_WithNoFilters_ShouldReturnResolvedOnesToo()
	{
		await AddAsync(minutesFromOrigin: 0);
		await AddAsync(minutesFromOrigin: 1, resolvedAt: Origin);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync();

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
	}

	[Test]
	public async Task GetAllAsync_WithTypeFilter_ShouldReturnOnlyThatType()
	{
		await AddAsync(minutesFromOrigin: 0, type: UnresolvableEventType.TransferCompensation);
		Guid outbox = await AddAsync(minutesFromOrigin: 1, type: UnresolvableEventType.OutboxDeadLetter);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync(type: UnresolvableEventType.OutboxDeadLetter);

		await Assert.That(value: result.Items.Select(selector: item => item.Id)).IsEquivalentTo(expected: new[] { outbox });
	}

	[Test]
	public async Task GetAllAsync_WithIsResolvedFalse_ShouldReturnOnlyTheOpenOnes()
	{
		Guid open = await AddAsync(minutesFromOrigin: 0);
		await AddAsync(minutesFromOrigin: 1, resolvedAt: Origin);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync(isResolved: false);

		await Assert.That(value: result.Items.Select(selector: item => item.Id)).IsEquivalentTo(expected: new[] { open });
	}

	[Test]
	public async Task GetAllAsync_WithIsAcknowledgedFalse_ShouldReturnOnlyTheUntouchedOnes()
	{
		Guid untouched = await AddAsync(minutesFromOrigin: 0);
		await AddAsync(minutesFromOrigin: 1, acknowledgedAt: Origin);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync(isAcknowledged: false);

		await Assert.That(value: result.Items.Select(selector: item => item.Id)).IsEquivalentTo(expected: new[] { untouched });
	}

	[Test]
	public async Task GetAllAsync_WithBothFlags_ShouldNarrowToTakenButNotFinished()
	{
		await AddAsync(minutesFromOrigin: 0);
		Guid inProgress = await AddAsync(minutesFromOrigin: 1, acknowledgedAt: Origin);
		await AddAsync(minutesFromOrigin: 2, acknowledgedAt: Origin, resolvedAt: Origin);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync(isAcknowledged: true, isResolved: false);

		await Assert.That(value: result.Items.Select(selector: item => item.Id)).IsEquivalentTo(expected: new[] { inProgress }).Because(message: """
			The two columns are independent, so combining them has to narrow rather than pick one. This is
			the state an operator asks for: someone took it and nobody has closed it.
		""");
	}

	[Test]
	public async Task GetAllAsync_WhenMoreRemain_ShouldReportAnotherPageWithoutLeakingTheProbeRow()
	{
		for (int minute = 0; minute < 5; minute++)
			await AddAsync(minutesFromOrigin: minute);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync(pageSize: 2);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.HasNextPage).IsTrue();
		await Assert.That(value: result.NextCursorId).IsEqualTo(expected: result.Items[^1].Id);
		await Assert.That(value: result.NextCursorDate).IsEqualTo(expected: result.Items[^1].OccurredAt);
	}

	[Test]
	public async Task GetAllAsync_WhenExactlyOnePageRemains_ShouldNotClaimAnotherPage()
	{
		for (int minute = 0; minute < 2; minute++)
			await AddAsync(minutesFromOrigin: minute);

		PagedResult<EventReadModel> result = await Repository.GetAllAsync(pageSize: 2);

		await Assert.That(value: result.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: result.HasNextPage).IsFalse()
			.Because(message: "a full page is not evidence of a further one, and claiming otherwise costs an empty round trip");

		await Assert.That(value: result.NextCursorId).IsNull();
		await Assert.That(value: result.NextCursorDate).IsNull();
	}

	[Test]
	public async Task GetAllAsync_WithCursor_ShouldWalkForwardWithoutRepeating()
	{
		for (int minute = 0; minute < 5; minute++)
			await AddAsync(minutesFromOrigin: minute);

		PagedResult<EventReadModel> firstPage = await Repository.GetAllAsync(pageSize: 3);

		PagedResult<EventReadModel> secondPage = await Repository.GetAllAsync(
			cursorOccurredAt: firstPage.NextCursorDate,
			cursorId: firstPage.NextCursorId,
			pageSize: 3
		);

		await Assert.That(value: secondPage.Items.Count).IsEqualTo(expected: 2);
		await Assert.That(value: secondPage.HasNextPage).IsFalse();
		await Assert.That(value: secondPage.Items.Any(predicate: item => firstPage.Items.Any(predicate: seen => seen.Id == item.Id))).IsFalse().Because(message: """
			The list runs ascending, so the cursor has to compare greater-than. Reusing the descending
			comparison from the user-facing lists would hand back the page that was just read.
		""");
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
