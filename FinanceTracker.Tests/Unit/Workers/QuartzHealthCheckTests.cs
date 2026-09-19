using FinanceTracker.Worker.Shared.HealthCheck;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using NSubstitute;
using Quartz;

namespace FinanceTracker.Tests.Unit.Workers;

public sealed class QuartzHealthCheckTests
{
	private static async Task<HealthCheckResult> CheckAsync(SchedulerStatus status)
	{
		IScheduler scheduler = Substitute.For<IScheduler>();
		scheduler.Status.Returns(returnThis: status);

		ISchedulerFactory schedulerFactory = Substitute.For<ISchedulerFactory>();
		schedulerFactory.GetScheduler(cancellationToken: Arg.Any<CancellationToken>()).Returns(returnThis: scheduler);

		return await new QuartzHealthCheck(schedulerFactory: schedulerFactory).CheckHealthAsync(
			context: new HealthCheckContext(),
			ct: CancellationToken.None
		);
	}

	[Test]
	public async Task ARunningSchedulerIsHealthy()
	{
		HealthCheckResult result = await CheckAsync(status: SchedulerStatus.Running);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Healthy);
	}

	[Test]
	public async Task ASchedulerThatNeverStartedIsUnhealthy()
	{
		HealthCheckResult result = await CheckAsync(status: SchedulerStatus.Created);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Unhealthy)
			.Because(message: "the host is up but nothing will ever fire, which is worse than being down because it looks fine");
	}

	[Test]
	public async Task AShutDownSchedulerIsUnhealthy()
	{
		HealthCheckResult result = await CheckAsync(status: SchedulerStatus.Shutdown);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Unhealthy);
	}

	[Test]
	public async Task ASchedulerInStandbyIsUnhealthy()
	{
		HealthCheckResult result = await CheckAsync(status: SchedulerStatus.Standby);

		await Assert.That(value: result.Status).IsEqualTo(expected: HealthStatus.Unhealthy)
			.Because(message: "a scheduler in standby holds its triggers and fires nothing, so the worker is up and doing no work");
	}

	[Test]
	public async Task AnUnhealthyResultSaysWhat()
	{
		HealthCheckResult result = await CheckAsync(status: SchedulerStatus.Created);

		await Assert.That(value: result.Description).IsNotNull()
			.Because(message: "the description is what reaches the readiness payload and the on-call engineer reading it");
	}
}
