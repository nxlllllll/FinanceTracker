using FinanceTracker.Worker.Shared.Job;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class TestJobOptions : IJobOptions
{
	public bool IsEnabled { get; init; } = true;
}

public sealed class TrackingJob(
	IOptionsMonitor<TestJobOptions> options
) : BaseJob<TestJobOptions>(options: options, logger: NullLogger<TrackingJob>.Instance)
{
	public int ProcessCallCount { get; private set; }
	public TestJobOptions? LastOptions { get; private set; }
	public CancellationToken LastCancellationToken { get; private set; }

	protected override Task ProcessAsync(TestJobOptions options, CancellationToken ct)
	{
		ProcessCallCount++;
		LastOptions = options;
		LastCancellationToken = ct;
		return Task.CompletedTask;
	}
}

public sealed class BaseJobTests
{
	private static IJobExecutionContext BuildContext(CancellationToken ct = default)
	{
		IJobExecutionContext context = Substitute.For<IJobExecutionContext>();
		context.CancellationToken.Returns(returnThis: ct);
		return context;
	}

	private static TrackingJob BuildJob(bool isEnabled)
	{
		TestJobOptions opts = new TestJobOptions { IsEnabled = isEnabled };
		IOptionsMonitor<TestJobOptions> monitor = Substitute.For<IOptionsMonitor<TestJobOptions>>();
		monitor.CurrentValue.Returns(returnThis: opts);
		return new TrackingJob(options: monitor);
	}

	[Test]
	public async Task Execute_WhenEnabled_ShouldCallProcessAsync()
	{
		TrackingJob job = BuildJob(isEnabled: true);

		await job.Execute(context: BuildContext());

		await Assert.That(value: job.ProcessCallCount).IsEqualTo(expected: 1);
	}

	[Test]
	public async Task Execute_WhenDisabled_ShouldNotCallProcessAsync()
	{
		TrackingJob job = BuildJob(isEnabled: false);

		await job.Execute(context: BuildContext());

		await Assert.That(value: job.ProcessCallCount).IsEqualTo(expected: 0);
	}

	[Test]
	public async Task Execute_WhenEnabled_ShouldPassOptionsToProcessAsync()
	{
		TrackingJob job = BuildJob(isEnabled: true);

		await job.Execute(context: BuildContext());

		await Assert.That(value: job.LastOptions).IsNotNull();
		await Assert.That(value: job.LastOptions!.IsEnabled).IsTrue();
	}

	[Test]
	public async Task Execute_WhenEnabled_ShouldPassCancellationTokenToProcessAsync()
	{
		TrackingJob job = BuildJob(isEnabled: true);
		using CancellationTokenSource cts = new CancellationTokenSource();

		await job.Execute(context: BuildContext(ct: cts.Token));

		await Assert.That(value: job.LastCancellationToken).IsEqualTo(expected: cts.Token);
	}

	[Test]
	public async Task Execute_WhenDisabledThenEnabled_ShouldRespectCurrentValue()
	{
		TestJobOptions disabledOpts = new TestJobOptions { IsEnabled = false };
		TestJobOptions enabledOpts = new TestJobOptions { IsEnabled = true };

		IOptionsMonitor<TestJobOptions> monitor = Substitute.For<IOptionsMonitor<TestJobOptions>>();
		monitor.CurrentValue.Returns(returnThis: disabledOpts, returnThese: enabledOpts);

		TrackingJob job = new TrackingJob(options: monitor);

		await job.Execute(context: BuildContext());
		await job.Execute(context: BuildContext());

		await Assert.That(value: job.ProcessCallCount).IsEqualTo(expected: 1);
	}
}