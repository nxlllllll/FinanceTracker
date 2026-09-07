using FinanceTracker.Core.Repositories.UnresolvableEvent;
using FinanceTracker.Tests.Unit.Helpers;
using FinanceTracker.Worker.DeadLetterMonitor.Job;
using FinanceTracker.Worker.Shared.Metrics;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

[NotInParallel]
public sealed class UnresolvableEventGaugeJobTests
{
	private const string PendingInstrument = "unresolvable_events.pending";

	private IUnresolvableEventReadRepository _readRepository = null!;
	private IJobExecutionContext _jobContext = null!;
	private UnresolvableEventGaugeJob _job = null!;

	[Before(hookType: Test)]
	public void Setup()
	{
		_readRepository = Substitute.For<IUnresolvableEventReadRepository>();
		_jobContext = Substitute.For<IJobExecutionContext>();

		_jobContext.CancellationToken.Returns(returnThis: CancellationToken.None);

		_job = new UnresolvableEventGaugeJob(
			unresolvableEventReadRepository: _readRepository,
			options: new FakeOptionsMonitor<UnresolvableEventGaugeOptions>(new UnresolvableEventGaugeOptions()),
			logger: new CapturingLogger<UnresolvableEventGaugeJob>()
		);
	}

	private static MetricCollector CollectPending()
		=> MetricCollector.ForMeter(meterName: WorkerMetrics.MeterName, instrumentNames: PendingInstrument);

	[Test]
	public async Task Execute_ShouldPublishTheUnresolvedCount()
	{
		_readRepository.CountUnresolvedAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: 7);

		using MetricCollector collector = CollectPending();

		await _job.Execute(context: _jobContext);

		await Assert.That(value: collector.Total(instrument: PendingInstrument)).IsEqualTo(expected: 7).Because(message: """
			This job exists only to publish this number: two Prometheus alerts and the overview dashboard
			read it, and nothing else writes it. A run that queries but never records is a silent outage
			of the alerting, not a missing log line.
		""");
	}

	[Test]
	public async Task Execute_WithAnEmptyBacklog_ShouldPublishZeroRatherThanNothing()
	{
		_readRepository.CountUnresolvedAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: 0);

		using MetricCollector collector = CollectPending();

		await _job.Execute(context: _jobContext);

		await Assert.That(value: collector.For(instrument: PendingInstrument).Count).IsEqualTo(expected: 1).Because(message: """
			Skipping the record on an empty backlog would leave the last non-zero value as the newest
			sample, and the alert would keep firing after the queue was drained.
		""");
	}

	[Test]
	public async Task Execute_ShouldPassTheJobCancellationTokenToTheRepository()
	{
		using CancellationTokenSource cts = new CancellationTokenSource();
		_jobContext.CancellationToken.Returns(returnThis: cts.Token);

		_readRepository.CountUnresolvedAsync(ct: Arg.Any<CancellationToken>()).Returns(returnThis: 0);

		await _job.Execute(context: _jobContext);

		await _readRepository.Received(requiredNumberOfCalls: 1).CountUnresolvedAsync(ct: cts.Token);
	}
}
