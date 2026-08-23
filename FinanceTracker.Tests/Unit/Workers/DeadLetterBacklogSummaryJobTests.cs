using FinanceTracker.Core.Domains.Abstractions.UnresolvableEvent;
using FinanceTracker.Core.ReadModels.UnresolvableEvent;
using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.DeadLetterMonitor.Job;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class DeadLetterBacklogSummaryJobTests
{
	private IUnresolvableEventReadRepository _readRepository = null!;
	private CapturingLogger<DeadLetterBacklogSummaryJob> _logger = null!;
	private IJobExecutionContext _jobContext = null!;
	private DeadLetterBacklogSummaryJob _job = null!;

	private static readonly DeadLetterBacklogSummaryOptions DefaultOptions = new DeadLetterBacklogSummaryOptions
	{
		UnresolvedOlderThanHours = 24,
		SampleSize = 5
	};

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IUnresolvableEventReadRepository>();
		_logger = new CapturingLogger<DeadLetterBacklogSummaryJob>();
		_jobContext = Substitute.For<IJobExecutionContext>();
		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_job = new DeadLetterBacklogSummaryJob(
			unresolvableEventReadRepository: _readRepository,
			dateProvider: FakeDateProvider.Default,
			options: new FakeOptionsMonitor<DeadLetterBacklogSummaryOptions>(DefaultOptions),
			logger: _logger
		);
	}

	private static UnresolvableEvent BuildDto() => new UnresolvableEvent(
		Id: Guid.CreateVersion7(),
		Type: UnresolvableEventType.OutboxDeadLetter,
		ReferenceId: Guid.CreateVersion7(),
		Reason: "Max retries exceeded.",
		OccurredAt: FakeDateProvider.Default.UtcNow.AddDays(days: -2),
		AcknowledgedAt: FakeDateProvider.Default.UtcNow.AddDays(days: -2),
		ResolvedAt: null
	);

	[Test]
	public async Task Execute_WhenNoBacklog_ShouldNotLogWarning()
	{
		_readRepository.GetUnresolvedOlderThanAsync(
			cutoff: Arg.Any<DateTimeOffset>(),
			sampleSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new UnresolvedBacklogSummary(TotalCount: 0, OldestOccurredAt: null, Sample: []));

		await _job.Execute(context: _jobContext);

		await Assert.That(value: _logger.WarningLogged).IsFalse();
	}

	[Test]
	public async Task Execute_WhenBacklogExists_ShouldLogWarning()
	{
		UnresolvableEvent e1 = BuildDto();
		_readRepository.GetUnresolvedOlderThanAsync(
			cutoff: Arg.Any<DateTimeOffset>(),
			sampleSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new UnresolvedBacklogSummary(TotalCount: 1, OldestOccurredAt: e1.OccurredAt, Sample: [e1]));

		await _job.Execute(context: _jobContext);

		await Assert.That(value: _logger.WarningLogged).IsTrue();
	}

	[Test]
	public async Task Execute_WhenBacklogExists_ShouldLogOneLinePerSampleEvent()
	{
		UnresolvableEvent e1 = BuildDto();
		UnresolvableEvent e2 = BuildDto();
		_readRepository.GetUnresolvedOlderThanAsync(
			cutoff: Arg.Any<DateTimeOffset>(),
			sampleSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new UnresolvedBacklogSummary(TotalCount: 2, OldestOccurredAt: e1.OccurredAt, Sample: [e1, e2]));

		await _job.Execute(context: _jobContext);

		// 1 summary line + 1 line per sampled event.
		await Assert.That(value: _logger.LogCount).IsEqualTo(expected: 3);
	}

	[Test]
	public async Task Execute_ShouldPassCutoffComputedFromOptions()
	{
		_readRepository.GetUnresolvedOlderThanAsync(
			cutoff: Arg.Any<DateTimeOffset>(),
			sampleSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new UnresolvedBacklogSummary(TotalCount: 0, OldestOccurredAt: null, Sample: []));

		await _job.Execute(context: _jobContext);

		DateTimeOffset expectedCutoff = FakeDateProvider.Default.UtcNow.AddHours(hours: -DefaultOptions.UnresolvedOlderThanHours);

		await _readRepository.Received(requiredNumberOfCalls: 1).GetUnresolvedOlderThanAsync(
			cutoff: expectedCutoff,
			sampleSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		);
	}

	[Test]
	public async Task Execute_ShouldPassSampleSizeFromOptions()
	{
		_readRepository.GetUnresolvedOlderThanAsync(
			cutoff: Arg.Any<DateTimeOffset>(),
			sampleSize: Arg.Any<int>(),
			ct: Arg.Any<CancellationToken>()
		).Returns(returnThis: new UnresolvedBacklogSummary(TotalCount: 0, OldestOccurredAt: null, Sample: []));

		await _job.Execute(context: _jobContext);

		await _readRepository.Received(requiredNumberOfCalls: 1).GetUnresolvedOlderThanAsync(
			cutoff: Arg.Any<DateTimeOffset>(),
			sampleSize: DefaultOptions.SampleSize,
			ct: Arg.Any<CancellationToken>()
		);
	}
}
